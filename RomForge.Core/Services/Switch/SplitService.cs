using Common;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.NSZ;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using NSW.Core;
using NSW.Core.Enums;
using NSW.Core.Models;
using NSW.Core.Utils;
using System.IO;
using Path = System.IO.Path;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public class SplitService
{
    public static async Task<int> Split(string sourcePath, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, bool outputAsXci, int index, int groupCount, 
        IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, IReadOnlySet<string>? onlyTitleIds = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet.Clone();
        var allMetas = MetadataReader.GetMetadataFromContainer(keySet, sourcePath);
        var disposables = new List<IDisposable>();
        int successCount = 0;

        try
        {
            var storage = new LocalStorage(sourcePath, FileAccess.Read);

            disposables.Add(storage);

            string inputExt = Path.GetExtension(sourcePath).ToLowerInvariant();
            IFileSystem fs;
            List<(string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize)>? rootEntriesTemplate = null;
            byte[]? xciPrefixBuffer = null;
            Stream? srcStream = null;

            if (inputExt is ".xci" or ".xcz")
            {
                var xci = new Xci(keySet, storage);
                var rootPartition = xci.OpenPartition(XciPartitionType.Root);

                disposables.Add(rootPartition);

                fs = xci.OpenPartition(XciPartitionType.Secure);

                disposables.Add(fs);

                rootEntriesTemplate = [.. rootPartition
                    .EnumerateEntries("/", "*")
                    .Select(e =>
                    {
                        var (absOffset, size, hash, hashTargetSize) = rootPartition.GetEntryInfo(e.Name.ToString());
                        return (e.Name.ToString(), (ulong)absOffset, (ulong)size, hash, hashTargetSize);
                    })];

                xciPrefixBuffer = XciWriter.BuildPrefix([sourcePath]);
                srcStream = storage.AsStream();
            }
            else
            {
                fs = storage.OpenFileSystem(keySet, sourcePath);

                disposables.Add(fs);
            }

            if (outputAsXci)
            {
                xciPrefixBuffer ??= XciWriter.BuildPrefix([]);
                rootEntriesTemplate ??= XciWriter.DummyRootEntries();
            }

            keySet.RegisterTickets(fs);

            var fileRegistry = new Dictionary<string, (string EntryName, string Ext)>(StringComparer.OrdinalIgnoreCase);
            var tikRegistry = new Dictionary<string, (string EntryName, string Ext)>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in fs.EnumerateEntries("/", "*"))
            {
                string entryName = entry.Name.ToString();
                string entryExt = Path.GetExtension(entryName).ToLowerInvariant();

                if (entryExt is ".tik" or ".cert")
                {
                    tikRegistry[entryName] = (entryName, entryExt);
                    continue;
                }

                string finalName = entryExt == ".ncz" ? Path.ChangeExtension(entryName, ".nca") : entryName;

                if (!fileRegistry.TryGetValue(finalName, out var value) || (value.Ext == ".ncz" && entryExt == ".nca"))
                    fileRegistry[finalName] = (entryName, entryExt);
            }

            string cachedBaseTitle = allMetas.FirstOrDefault(m => m.Type == ContentMetaType.Application) 
                is { } appMeta ? (!string.IsNullOrEmpty(appMeta.KrTitle) ? appMeta.KrTitle : appMeta.EnTitle) : allMetas.FirstOrDefault()
                is { } firstMeta ? (!string.IsNullOrEmpty(firstMeta.KrTitle) ? firstMeta.KrTitle : firstMeta.EnTitle) : string.Empty;

            foreach (var meta in allMetas)
            {
                ct.ThrowIfCancellationRequested();

                if (onlyTitleIds != null && !onlyTitleIds.Contains(meta.TitleId))
                    continue;

                if (await ProcessSplitItem(meta, fileRegistry, tikRegistry, fs, keySet, cachedBaseTitle, outputDir, useCompression, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, outputAsXci, xciPrefixBuffer, rootEntriesTemplate, srcStream, index, groupCount, progress, log, ct))
                    successCount++;
            }
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--)
                disposables[i]?.Dispose();
        }

        return successCount;
    }

    private static async Task<bool> ProcessSplitItem(MetadataResult meta, Dictionary<string, (string EntryName, string Ext)> fileRegistry, Dictionary<string, (string EntryName, string Ext)> tikRegistry, IFileSystem fs, KeySet keySet, string baseTitle, string outputDir, 
        bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, bool outputAsXci, byte[]? xciPrefixBuffer, List<(string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize)>? rootEntriesTemplate, Stream? srcStream, int index, int groupCount, 
        IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        var disposables = new List<IDisposable>();
        var converters = new Dictionary<string, NcaToNczConverter>(StringComparer.OrdinalIgnoreCase);
        string? finalPath = null;
        bool isCompleted = false;

        try
        {
            string typeTag = meta.GetTypeTag();

            log?.Invoke($"{string.Format(Res.Log_SplitPreparing, $"{baseTitle} [{typeTag}]")} ({index}/{groupCount})", LogLevel.Highlight, meta.TitleId);

            if (!fileRegistry.TryGetValue(meta.FileName, out var cnmtEntry))
                return false;

            var cnmtFileRef = new UniqueRef<IFile>();

            if (fs.OpenFile(ref cnmtFileRef.Ref, ("/" + cnmtEntry.EntryName).ToU8Span(), OpenMode.Read).IsFailure())
                return false;

            IFile cnmtRawFile = cnmtFileRef.Release();

            disposables.Add(cnmtRawFile);

            var cnmtNcaStorage = new FileStorage(cnmtRawFile);

            disposables.Add(cnmtNcaStorage);

            var cnmtNca = new Nca(keySet, cnmtNcaStorage);
            using var cnmtFs = cnmtNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
            var cnmtFsEntry = cnmtFs.EnumerateEntries("/", "*.cnmt").First();
            using var cFile = new UniqueRef<IFile>();

            cnmtFs.OpenFile(ref cFile.Ref, cnmtFsEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

            var cnmt = new Cnmt(cFile.Get.AsStream());
            var fileEntries = new List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)>();
            string titleIdHex = meta.TitleId.ToUpper();

            if (!forceKeyGen0)
            {
                foreach (var kvp in tikRegistry)
                {
                    if (!kvp.Key.Contains(titleIdHex, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var fileRef = new UniqueRef<IFile>();

                    if (fs.OpenFile(ref fileRef.Ref, ("/" + kvp.Value.EntryName).ToU8Span(), OpenMode.Read).IsFailure())
                        continue;

                    IFile rawFile = fileRef.Release();

                    disposables.Add(rawFile);

                    IStorage tikStorage = new FileStorage(rawFile);

                    disposables.Add(tikStorage);
                    tikStorage.GetSize(out long tikSize).ThrowIfFailure();

                    var cap = tikStorage;

                    fileEntries.Add((kvp.Value.EntryName, async (s, onRead) => await Common.Utils.CopyStreamAsync(cap.AsStream(), s, onRead, ct), tikSize, kvp.Value.EntryName));
                }
            }

            cnmtNcaStorage.GetSize(out long cnmtSize).ThrowIfFailure();

            var cnmtCap = cnmtNcaStorage;

            fileEntries.Add((cnmtEntry.EntryName, async (s, onRead) =>
            {
                await NcaRecryptService.RecryptAsync(cnmtCap.AsStream(), s, forceKeyGen0 ? 0 : cnmtNca.Header.KeyGeneration, keySet, onRead, ct);
            }, cnmtSize, cnmtEntry.EntryName));

            foreach (var record in cnmt.ContentEntries)
            {
                ct.ThrowIfCancellationRequested();

                string targetId = Convert.ToHexStringLower(record.NcaId);
                string? ncaKey = fileRegistry.Keys.FirstOrDefault(k => Path.GetFileNameWithoutExtension(k).StartsWith(targetId, StringComparison.OrdinalIgnoreCase));

                if (ncaKey == null) 
                    continue;

                var (entryName, originalExt) = fileRegistry[ncaKey];
                var fileRef = new UniqueRef<IFile>();

                if (fs.OpenFile(ref fileRef.Ref, ("/" + entryName).ToU8Span(), OpenMode.Read).IsFailure())
                    continue;

                IFile rawFile = fileRef.Release();

                disposables.Add(rawFile);

                IStorage currentStorage = new FileStorage(rawFile);

                disposables.Add(currentStorage);
                currentStorage.GetSize(out long size).ThrowIfFailure();

                var nca = new Nca(keySet, currentStorage);
                string ncaContentType = nca.Header.ContentType.ToString();
                string label = $"{baseTitle} [{typeTag}/{ncaContentType}]";
                var result = ContainerEntryBuilder.BuildNcaEntry(entryName, originalExt, currentStorage, size, nca, label, useCompression, useBlockMode, compressionLevel, forceKeyGen0, keySet, converters, null, ct);

                if (result == null) 
                    continue;

                log?.Invoke($"- {label} {(result.Value.FinalName.EndsWith(".ncz") ? Res.Log_CompressAndSplit : Res.Log_Splitting)}", LogLevel.Info, meta.TitleId);
                fileEntries.Add((result.Value.FinalName, result.Value.Writer, result.Value.Size, result.Value.Label));
            }

            ContainerFormat format = outputAsXci ? (useCompression ? ContainerFormat.Xcz : ContainerFormat.Xci) : (useCompression ? ContainerFormat.Nsz : ContainerFormat.Nsp);
            string outName = NameBuilder.SplitFileNameBuild(meta.KrTitle, meta.EnTitle, meta.TitleId, meta.GetEffectiveDisplayVersion(), typeTag, format);

            finalPath = Utils.GetUniqueFilePath(Path.Combine(outputDir, outName));

            string displayName = NameBuilder.DisplayNameBuild(meta.EnTitle, meta.TitleId, meta.DisplayVersion, meta.DlcCount, format);            
            using var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

            if (outputAsXci)
                await XciWriter.WriteAsync(displayName, meta.TitleId, xciPrefixBuffer!, rootEntriesTemplate!, srcStream, fileEntries, fout, progress, ct);
            else
            {
                await Pfs0Builder.WriteAsync($"{Res.Log_Splitting} {displayName}", meta.TitleId, fileEntries, fout, 0x20, false, null, progress, ct);

                if (useCompression && converters.Count > 0 && isValidationEnabled)
                {
                    log?.Invoke($"{baseTitle} [{typeTag}] {Res.Log_ValidationStart} ({index}/{groupCount})", LogLevel.Highlight, meta.TitleId);

                    var validationFs = OutputContainer.AsNsp.OpenForValidation(fout, keySet);

                    await NczValidator.ValidateAsync(validationFs, converters, fileEntries, meta.TitleId, _ => $"{baseTitle} [{typeTag}]", Res.ToolTip_ValidateCompress, "OK", progress, log, meta.TitleId, ct);
                    log?.Invoke($"{baseTitle} [{typeTag}] {Res.Log_ValidationComplete} ({index}/{groupCount})", LogLevel.Ok, meta.TitleId);
                }
            }

            isCompleted = true;

            log?.Invoke($"{string.Format(Res.Log_SplitComplete, outName)} ({index}/{groupCount})", LogLevel.Ok, meta.TitleId);

            return true;
        }
        catch (OperationCanceledException)
        { 
            throw;
        }
        catch (Exception ex)
        {
            log?.Invoke($"{string.Format(Res.Log_SplitFailed, meta.TitleId, ex.Message)} ({index}/{groupCount})", LogLevel.Error, meta.TitleId);
            return false;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--) 
                disposables[i]?.Dispose();

            if (!isCompleted) 
                IncompleteOutput.Delete(finalPath, log, meta.TitleId);
        }
    }
}