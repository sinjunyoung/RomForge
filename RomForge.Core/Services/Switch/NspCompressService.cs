using Common;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.NSZ;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Utils;
using System.IO;
using Path = System.IO.Path;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public static class NspCompressService
{
    public static Task<string> CompressAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunCoreAsync(inputPath, true, compressionLevel, validation, useBlockMode, false, keySet?.Clone(), progress, log, outputDir, ct);
    }

    public static Task<string> DecompressAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunCoreAsync(inputPath, false, 0, false, false, false, keySet?.Clone(), progress, log, outputDir, ct);
    }

    private static async Task<string> RunCoreAsync(string inputPath, bool isCompressMode, int compressionLevel, bool validation, bool useBlockMode, bool forceKeyGen0, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir, CancellationToken ct)
    {
        var disposables = new List<IDisposable>();
        var converters = new Dictionary<string, NcaToNczConverter>(StringComparer.OrdinalIgnoreCase);
        string? finalPath = null;
        bool isCompleted = false;
        string modeDone = isCompressMode ? Res.Log_ModeCompress : Res.Log_ModeDecompress;

        log?.Invoke($"{Path.GetFileName(inputPath)} {modeDone} {Res.Log_ProcessStart}", LogLevel.Info, inputPath);

        try
        {
            var metas = MetadataReader.GetMetadataFromContainer(keySet, inputPath);

            if (metas.Count == 0)
                throw new InvalidOperationException(Res.Error_NoMetadata);

            var meta = metas.First();
            var sourceStorage = new LocalStorage(inputPath, FileAccess.Read);

            disposables.Add(sourceStorage);

            IFileSystem sourceFs = sourceStorage.OpenFileSystem(keySet, inputPath);

            disposables.Add(sourceFs);
            keySet.RegisterTickets(sourceFs);

            string outputExt = isCompressMode ? ".nsz" : ".nsp";

            finalPath = Utils.GetUniqueFilePath(Utils.ResolveOutputFilePath(inputPath, outputExt, outputDir));

            var fileEntries = new List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)>();

            foreach (var entry in sourceFs.EnumerateEntries("/", "*"))
            {
                string entryName = entry.Name.ToString();
                string entryExt = Path.GetExtension(entryName).ToLowerInvariant();
                var fileRef = new UniqueRef<IFile>();

                if (!sourceFs.OpenFile(ref fileRef.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsSuccess())
                    continue;

                fileRef.Get.GetSize(out long size).ThrowIfFailure();

                IFile rawFile = fileRef.Release();

                disposables.Add(rawFile);

                IStorage currentStorage = new FileStorage(rawFile);

                disposables.Add(currentStorage);

                if (entryExt is ".tik" or ".cert")
                {
                    if (!forceKeyGen0)
                    {
                        var capturedStorage = currentStorage;

                        fileEntries.Add((entryName, async (s, onRead) => await Utils.CopyStreamAsync(capturedStorage.AsStream(), s, onRead, ct), size, entryName));
                    }
                    continue;
                }

                if (entryExt is not ".nca" and not ".ncz")
                {
                    var capturedStorage = currentStorage;

                    fileEntries.Add((entryName, async (s, onRead) => await Utils.CopyStreamAsync(capturedStorage.AsStream(), s, onRead, ct), size, entryName));
                    continue;
                }

                IStorage ncaStorage;
                string ncaName;
                long ncaSize;

                if (entryExt == ".ncz")
                {
                    var ncz = new Ncz(keySet, currentStorage.AsStream(), NczReadMode.Original);

                    ncz.BaseStorage.GetSize(out ncaSize).ThrowIfFailure();

                    ncaStorage = ncz.BaseStorage;
                    ncaName = Path.ChangeExtension(entryName, ".nca");
                }
                else
                {
                    ncaStorage = currentStorage;
                    ncaName = entryName;
                    ncaSize = size;
                }

                var nca = new Nca(keySet, ncaStorage);
                string label = $"{meta.KrTitle ?? meta.EnTitle} [{nca.Header.ContentType}]";

                if (isCompressMode && nca.Header.ContentType is NcaContentType.Program or NcaContentType.PublicData)
                {
                    string compName = nca.HasSparseLayer() ? ncaName : Path.ChangeExtension(ncaName, ".ncz");
                    var converter = new NcaToNczConverter(keySet);

                    converters[ncaName] = converter;

                    var capturedStorage = ncaStorage;

                    fileEntries.Add((compName, async (s, onRead) =>
                    {
                        var recryptedHeader = await NcaRecryptService.GetRecryptedHeaderAsync(capturedStorage, (int)nca.Header.KeyGeneration, keySet, ct);
                        using var headerStream = new MemoryStream(recryptedHeader);

                        await converter.ConvertAsync(headerStream, capturedStorage, s, useBlockMode, compressionLevel, onRead, ct);
                    }, size, label));
                }
                else
                {
                    var capturedStorage = ncaStorage;
                    string statusLabel = isCompressMode ? Res.Log_StatusCopying : Res.Log_StatusDecompressing;

                    fileEntries.Add((ncaName, async (s, onRead) =>
                    {
                        await NcaRecryptService.RecryptAsync(capturedStorage.AsStream(), s, (int)nca.Header.KeyGeneration, keySet, onRead, ct);
                    }, ncaSize, $"{label} [{statusLabel}]"));
                }
            }

            string displayName = $"{(isCompressMode ? Res.Log_StatusCompressing : Res.Log_StatusDecompressing)} {NspNameBuilder.CompressDisplayNameBuild(meta.KrTitle, meta.TitleId, meta.DisplayVersion)}";
            var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

            disposables.Add(fout);

            await Pfs0Builder.WriteAsync(displayName, Path.GetFileNameWithoutExtension(finalPath), fileEntries, fout, Pfs0Builder.GetAlignmentPadding(inputPath), progress, ct);

            if (isCompressMode)
            {
                long originalSize = new FileInfo(inputPath).Length;
                long compressedSize = fout.Length;

                log?.Invoke($"{Res.Log_CompressionRatio}: {Utils.FormatFileSize(originalSize)} → {Utils.FormatFileSize(compressedSize)} ({compressedSize * 100.0 / originalSize:F1}%)", LogLevel.Highlight, meta.TitleId);

                if (validation)
                {
                    fout.Position = 0;

                    var validationPfs = new PartitionFileSystem();

                    validationPfs.Initialize(fout.AsStorage()).ThrowIfFailure();

                    IFileSystem validationFs = validationPfs;
                    var nczEntries = validationFs.EnumerateEntries("/", "*.ncz")
                        .Where(e => converters.ContainsKey(Path.ChangeExtension(e.Name, ".nca")))
                        .ToList();
                    long totalValidationSize = nczEntries.Sum(e => fileEntries.FirstOrDefault(f => string.Equals(f.Name, e.Name, StringComparison.OrdinalIgnoreCase)).EstimatedSize);

                    foreach (var entry in nczEntries)
                    {
                        ct.ThrowIfCancellationRequested();

                        string origName = Path.ChangeExtension(entry.Name, ".nca");

                        if (!converters.TryGetValue(origName, out var converter))
                            continue;

                        using var nczFile = new UniqueRef<IFile>();

                        validationFs.OpenFile(ref nczFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                        string label = $"{meta.KrTitle ?? meta.EnTitle} [{meta.TitleId}]";

                        log?.Invoke($"- {label} {Res.Log_StatusValidating}", LogLevel.Info, meta.TitleId);
                        await converter.ValidateAsync(nczFile.Get.AsStream(), Path.GetFileNameWithoutExtension(finalPath), totalValidationSize, label, progress, ct);
                        log?.Invoke($"- {label} {Res.Log_ValidationComplete}", LogLevel.Ok, meta.TitleId);
                    }
                }
            }

            isCompleted = true;

            log?.Invoke($"{modeDone} {Res.Log_StatusDone}: {Path.GetFileName(finalPath)}", LogLevel.Ok, meta.TitleId);

            return finalPath;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--) disposables[i]?.Dispose();

            if (!isCompleted && !string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
                try
                {
                    File.Delete(finalPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"실패한 결과 파일 삭제 실패 ({finalPath}): {ex.Message}");
                }
        }
    }
}