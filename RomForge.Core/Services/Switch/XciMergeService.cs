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
using NSW.Core;
using NSW.Core.Enums;
using NSW.Core.Models;
using NSW.Core.Utils;
using System.IO;
using Path = System.IO.Path;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public class XciMergeService : BaseSwitchService
{
    public static async Task<List<string>> Merge(IReadOnlyList<string> inputPaths, string outputDir, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
        => await RunMergeAll(inputPaths, outputDir, compressionLevel > 0, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, KeySetProvider.Instance.KeySet.Clone(), progress, log, ct);

    public static async Task<List<string>> RunMergeAll(IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, KeySet keySet, 
        IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        log?.Invoke(Res.Log_AnalyzeMetadata, LogLevel.Info, string.Empty);

        var allMeta = new List<MetadataResult>();

        foreach (var path in inputPaths)
        {
            ct.ThrowIfCancellationRequested();

            allMeta.AddRange(MetadataReader.GetMetadataFromContainer(keySet, path));
        }

        if (allMeta.Count == 0)
            throw new InvalidOperationException(Res.Error_NoMetadata);

        var groups = BuildTitleGroups(allMeta);

        log?.Invoke(string.Format(Res.Log_TitleGroupDetected, groups.Count), LogLevel.Info, string.Empty);

        var results = new List<string>();
        int idx = 0;

        foreach (var group in groups.Values)
        {
            ct.ThrowIfCancellationRequested();

            idx++;

            bool hasAnyContent = group.BaseMetas.Count > 0 || group.PatchMetas.Count > 0 || group.DlcMetas.Count > 0;

            if (!hasAnyContent)
                continue;

            var baseMeta = group.BaseMetas.FirstOrDefault() ?? group.PatchMetas.OrderByDescending(m => m.TitleVersion).FirstOrDefault() ?? group.DlcMetas.FirstOrDefault();

            if (baseMeta == null)
                continue;

            var allSources = group.BaseMetas
                .Concat(group.PatchMetas)
                .Concat(group.DlcMetas)
                .Select(m => m.SourcePath)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var latestPatch = group.PatchMetas
                .OrderByDescending(m => m.TitleVersion)
                .FirstOrDefault();
            var allowedNcaIds = BuildAllowedNcaIds(group, latestPatch);
            var req = new Models.Switch.BuildRequest(group.BaseMetas.FirstOrDefault()?.SourcePath ?? string.Empty, latestPatch?.SourcePath ?? string.Empty, [.. group.DlcMetas.Select(m => m.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase)], outputDir)
            {
                UseCompression = useCompression,
                CompressionLevel = compressionLevel,
                UseBlockMode = useBlockMode,
                AllSourcePaths = allSources,
                TargetBaseTitleId = group.BaseTitleId,
                TargetBaseTitleName = group.BaseTitleName,
                AllowedNcaIds = allowedNcaIds,
                ResolvedMeta = new MetadataResult(
                    baseMeta.TitleId, (latestPatch ?? baseMeta).TitleVersion, (latestPatch ?? baseMeta).DisplayVersion, baseMeta.KrTitle, baseMeta.EnTitle,
                    group.DlcMetas.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Count(), Type: group.BaseMetas.Count > 0 ? ContentMetaType.Application : baseMeta.Type),
            };

            log?.Invoke($"{group.BaseTitleName} {Res.Button_MergeStart} ({idx}/{groups.Count})", LogLevel.Highlight, group.BaseTitleId);

            try
            {
                var groupMeta = allMeta.Where(m =>
                {
                    string baseTid = LibHacHelper.GetBaseTitleId(m.TitleId);

                    if (string.IsNullOrEmpty(baseTid))
                        return false;

                    return baseTid.Equals(group.BaseTitleId, StringComparison.OrdinalIgnoreCase);

                }).ToList();

                results.Add(await RunMergeProcess(req, keySet, groupMeta, isValidationEnabled, forceKeyGen0, idx, groups.Count, group.BaseMetas.Count > 0, group.PatchMetas.Count > 0, progress, log, ct));
            }
            catch (OperationCanceledException) 
            { 
                throw; 
            }
            catch (Exception ex)
            {
                log?.Invoke(string.Format(Res.Log_MergeFailed, group.BaseTitleId, ex.Message), LogLevel.Error, group.BaseTitleId);
            }
        }

        return results;
    }

    public static async Task<List<string>> RunMergeSingle(IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, KeySet keySet,
        IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        log?.Invoke(Res.Log_AnalyzeMetadata, LogLevel.Info, string.Empty);

        var allMeta = new List<MetadataResult>();

        foreach (var path in inputPaths)
        {
            ct.ThrowIfCancellationRequested();

            allMeta.AddRange(MetadataReader.GetMetadataFromContainer(keySet, path));
        }

        if (allMeta.Count == 0)
            throw new InvalidOperationException(Res.Error_NoMetadata);

        var (req, hasBase, hasUpdate) = BuildSingleRequest(allMeta, inputPaths, outputDir, useCompression, compressionLevel, useBlockMode);

        log?.Invoke($"{req.TargetBaseTitleName} {Res.Button_MergeStart} (1/1)", LogLevel.Highlight, req.TargetBaseTitleId);

        var results = new List<string>();

        try
        {
            results.Add(await RunMergeProcess(req, keySet, allMeta, isValidationEnabled, forceKeyGen0, 1, 1, hasBase, hasUpdate, progress, log, ct));
        }
        catch (OperationCanceledException) 
        { 
            throw; 
        }
        catch (Exception ex)
        {
            log?.Invoke(string.Format(Res.Log_MergeFailed, req.TargetBaseTitleId, ex.Message), LogLevel.Error, req.TargetBaseTitleId);
        }

        return results;
    }

    public static async Task<string> RunMergeProcess(Models.Switch.BuildRequest req, KeySet keySet, List<MetadataResult> cachedMeta, bool isValidationEnabled, bool forceKeyGen0, int index, int groupCount, bool hasBase, bool hasUpdate, 
        IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var disposables = new List<IDisposable>();
        var converters = new Dictionary<string, NcaToNczConverter>(StringComparer.OrdinalIgnoreCase);
        string? finalPath = null;
        bool isCompleted = false;

        try
        {
            var allPaths = GetAllPaths(req);
            string? xciBasePath = allPaths.FirstOrDefault(p => Path.GetExtension(p).ToLowerInvariant() is ".xci" or ".xcz");
            byte[] xciPrefixBuffer = GetXciPrefix(allPaths);
            var ncaIdToMeta = cachedMeta
                .Where(m => m.ContentNcaIds != null)
                .SelectMany(m => m.ContentNcaIds!.Select(id => (id, m)))
                .GroupBy(x => x.id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().m, StringComparer.OrdinalIgnoreCase);
            var fileRegistry = new Dictionary<string, (string Path, string EntryName, string Ext)>(StringComparer.OrdinalIgnoreCase);
            List<(string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize)>? rootEntriesTemplate = null;
            var xciStorages = new List<(string Path, IFileSystem Secure)>();

            foreach (var path in allPaths)
            {
                ct.ThrowIfCancellationRequested();

                var storage = new LocalStorage(path, FileAccess.Read);

                disposables.Add(storage);

                string ext = Path.GetExtension(path).ToLowerInvariant();
                IFileSystem securePartition;

                if (ext is ".xci" or ".xcz")
                {
                    var xci = new Xci(keySet, storage);

                    var rootPartition = xci.OpenPartition(XciPartitionType.Root);

                    disposables.Add(rootPartition);

                    securePartition = xci.OpenPartition(XciPartitionType.Secure);

                    disposables.Add(securePartition);

                    rootEntriesTemplate ??= [.. rootPartition
                    .EnumerateEntries("/", "*")
                    .Select(e =>
                    {
                        var (absOffset, size, hash, hashTargetSize) = rootPartition.GetEntryInfo(e.Name.ToString());

                        return (e.Name.ToString(), (ulong)absOffset, (ulong)size, hash, hashTargetSize);
                    })];
                }
                else
                {
                    securePartition = storage.OpenFileSystem(keySet, path);
                    disposables.Add(securePartition);
                }

                keySet.RegisterTickets(securePartition);
                xciStorages.Add((path, securePartition));

                foreach (var entry in securePartition.EnumerateEntries("/", "*"))
                {
                    string entryName = entry.Name.ToString();
                    string entryExt = Path.GetExtension(entryName).ToLowerInvariant();

                    if (req.AllowedNcaIds != null && entryExt is ".nca" or ".ncz")
                    {
                        string ncaId = LibHacHelper.ExtractNcaId(entryName);

                        if (!string.IsNullOrEmpty(ncaId) && !req.AllowedNcaIds.Contains(ncaId))
                            continue;
                    }

                    if (entryExt is ".tik" or ".cert")
                    {
                        if (!forceKeyGen0)
                            fileRegistry[entryName] = (path, entryName, entryExt);

                        continue;
                    }

                    string finalName = entryExt == ".ncz" ? Path.ChangeExtension(entryName, ".nca") : entryName;

                    if (!fileRegistry.TryGetValue(finalName, out var value) || (value.Ext == ".ncz" && entryExt == ".nca"))
                        fileRegistry[finalName] = (path, entryName, entryExt);
                }
            }

            rootEntriesTemplate ??= GetDummyRootEntries();

            var fileEntries = new List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)>();
            var addedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in fileRegistry)
            {
                ct.ThrowIfCancellationRequested();

                var (sourcePath, entryName, originalExt) = kvp.Value;
                var (_, secureFs) = xciStorages.First(x => x.Path == sourcePath);
                var fileRef = new UniqueRef<IFile>();

                if (!secureFs.OpenFile(ref fileRef.Ref, ("/" + entryName).ToU8Span(), OpenMode.Read).IsSuccess())
                    continue;

                fileRef.Get.GetSize(out long size).ThrowIfFailure();

                if (size == 0)
                {
                    fileRef.Destroy();
                    continue;
                }

                IFile rawFile = fileRef.Release();

                disposables.Add(rawFile);

                IStorage currentStorage = new FileStorage(rawFile);

                disposables.Add(currentStorage);

                if (originalExt is not (".nca" or ".ncz"))
                {
                    if (!addedFileNames.Add(kvp.Key))
                        continue;

                    var cap = currentStorage;

                    fileEntries.Add((kvp.Key, async (s, onRead) => await Common.Utils.CopyStreamAsync(cap.AsStream(), s, onRead, ct), size, kvp.Key));

                    continue;
                }

                var nca = new Nca(keySet, currentStorage);

                ncaIdToMeta.TryGetValue(LibHacHelper.ExtractNcaId(entryName), out var metaInfo);

                string typeTag = metaInfo != null ? MetadataReader.GetContentMetaTypeTag(metaInfo.Type) : "Unknown";
                string ncaContentType = nca.Header.ContentType.ToString();
                string titleName = !string.IsNullOrEmpty(metaInfo?.KrTitle) ? metaInfo.KrTitle : !string.IsNullOrEmpty(metaInfo?.EnTitle) ? metaInfo.EnTitle : nca.Header.TitleId.ToString("X16");
                string label = $"{titleName} [{typeTag}/{ncaContentType}]";
                var result = BuildFileEntry(entryName, originalExt, currentStorage, size, nca, label, req.UseCompression, req.UseBlockMode, req.CompressionLevel, forceKeyGen0, keySet, converters, ct);

                if (result == null)
                    continue;

                if (!addedFileNames.Add(result.Value.FinalName))
                    continue;

                log?.Invoke($"- {label}", LogLevel.Info, req.TargetBaseTitleId);
                fileEntries.Add((result.Value.FinalName, result.Value.Writer, result.Value.Size, result.Value.Label));
            }

            var meta = req.ResolvedMeta ?? ExtractFinalMetadata(keySet, allPaths, req.TargetBaseTitleId);

            log?.Invoke(string.Format(Res.Log_FinalId, meta.TitleId, meta.DisplayVersion), LogLevel.Ok, req.TargetBaseTitleId);

            string finalFileName = NameBuilder.FileNameBuild("Merged", meta.KrTitle, meta.EnTitle, meta.TitleId, meta.DisplayVersion, meta.TitleVersion, meta.DlcCount, hasBase, hasUpdate, req.UseCompression ? ContainerFormat.Xcz : ContainerFormat.Xci);

            finalPath = Utils.GetUniqueFilePath(Path.Combine(req.OutputDir, finalFileName));

            string displayName = NameBuilder.DisplayNameBuild(meta.EnTitle, meta.TitleId, meta.DisplayVersion, meta.DlcCount, hasBase, hasUpdate, req.UseCompression ? ContainerFormat.Xcz : ContainerFormat.Xci);

            LocalStorage? baseStorage = xciBasePath != null ? new LocalStorage(xciBasePath, FileAccess.Read) : null;

            if (baseStorage != null)
                disposables.Add(baseStorage);

            using var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

            await WriteXciAsync(displayName, meta.TitleId, xciPrefixBuffer, rootEntriesTemplate, baseStorage?.AsStream(), fileEntries, fout, progress, ct);

            if (req.UseCompression && converters.Count > 0 && isValidationEnabled)
            {
                log?.Invoke($"{req.TargetBaseTitleName} {Res.Log_ValidationStart} ({index}/{groupCount})", LogLevel.Highlight, req.TargetBaseTitleId);

                fout.Position = 0;

                var validationStorage = new StreamStorage(fout, false);
                var validationXci = new Xci(keySet, validationStorage);
                var validationSecure = validationXci.OpenPartition(XciPartitionType.Secure);
                var nczEntries = validationSecure.EnumerateEntries("/", "*.ncz")
                    .Where(e => converters.ContainsKey(Path.ChangeExtension(e.Name, ".nca")))
                    .ToList();
                long totalValidationSize = nczEntries.Sum(e => e.Size);

                foreach (var entry in nczEntries)
                {
                    ct.ThrowIfCancellationRequested();

                    string origName = Path.ChangeExtension(entry.Name, ".nca");

                    if (!converters.TryGetValue(origName, out var converter))
                        continue;

                    using var nczFile = new UniqueRef<IFile>();

                    validationSecure.OpenFile(ref nczFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                    ncaIdToMeta.TryGetValue(LibHacHelper.ExtractNcaId(entry.Name), out var nczMetaInfo);

                    string nczTypeTag = nczMetaInfo != null ? MetadataReader.GetContentMetaTypeTag(nczMetaInfo.Type) : "Unknown";
                    string label = $"{(nczMetaInfo?.KrTitle ?? nczMetaInfo?.EnTitle ?? entry.Name)} [{nczTypeTag}]";

                    log?.Invoke($"- {label} {Res.ToolTip_ValidateCompress}", LogLevel.Info, req.TargetBaseTitleId);
                    await converter.ValidateAsync(nczFile.Get.AsStream(), nczMetaInfo?.TitleId, totalValidationSize, label, progress, ct);
                    log?.Invoke($"- {label} OK", LogLevel.Ok, req.TargetBaseTitleId);
                }

                log?.Invoke($"{req.TargetBaseTitleName} {Res.Log_ValidationComplete} ({index}/{groupCount})", LogLevel.Ok, req.TargetBaseTitleId);
            }

            isCompleted = true;

            log?.Invoke(string.Format($"{Res.Log_MergeComplete} ({index}/{groupCount})", finalFileName), LogLevel.Ok, req.TargetBaseTitleId);

            return finalPath;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            log?.Invoke(string.Format($"{Res.Log_Error} ({index}/{groupCount})", ex.Message), LogLevel.Error, req.TargetBaseTitleId);
            throw;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--)
                disposables[i]?.Dispose();

            if (!isCompleted)
                CleanupOnFailure(finalPath, log, req.TargetBaseTitleId);
        }
    }
}