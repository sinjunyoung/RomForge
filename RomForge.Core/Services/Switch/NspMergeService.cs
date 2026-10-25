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

public class NspMergeService : BaseSwitchService
{
    public static async Task<List<string>> Merge(IReadOnlyList<string> inputPaths, string outputDir, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
        => await RunMergeAll(inputPaths, outputDir, compressionLevel > 0, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, KeySetProvider.Instance.KeySet.Clone(), progress, log, ct);

    public static async Task<List<string>> RunMergeAll(IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
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

    public static async Task<List<string>> RunMergeSingle(IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
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

    public static async Task<string> RunMergeProcess(Models.Switch.BuildRequest req, KeySet keySet, List<MetadataResult> cachedMeta, bool isValidationEnabled, bool forceKeyGen0, int index, int groupCount, bool hasBase, bool hasUpdate, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var disposables = new List<IDisposable>();
        var converters = new Dictionary<string, NcaToNczConverter>(StringComparer.OrdinalIgnoreCase);
        string? finalPath = null;
        bool isCompleted = false;
        var fileRegistry = new Dictionary<string, (string Path, string EntryName, string Ext)>(StringComparer.OrdinalIgnoreCase);
        var fsCache = new Dictionary<string, IFileSystem>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var allPaths = GetAllPaths(req);
            var ncaIdToMeta = cachedMeta
                .Where(m => m.ContentNcaIds != null)
                .SelectMany(m => m.ContentNcaIds!.Select(id => (id, m)))
                .GroupBy(x => x.id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().m, StringComparer.OrdinalIgnoreCase);

            foreach (var path in allPaths)
            {
                ct.ThrowIfCancellationRequested();

                var storage = new LocalStorage(path, FileAccess.Read);

                disposables.Add(storage);

                IFileSystem fs = storage.OpenFileSystem(keySet, path);

                disposables.Add(fs);

                fsCache[path] = fs;

                keySet.RegisterTickets(fs);

                foreach (var entry in fs.EnumerateEntries("/", "*"))
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

            var fileEntries = new List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)>();
            var addedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in fileRegistry)
            {
                ct.ThrowIfCancellationRequested();

                var (sourcePath, entryName, originalExt) = kvp.Value;

                if (!fsCache.TryGetValue(sourcePath, out var fs))
                    continue;

                var fileRef = new UniqueRef<IFile>();

                if (!fs.OpenFile(ref fileRef.Ref, ("/" + entryName).ToU8Span(), OpenMode.Read).IsSuccess())
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

                    fileEntries.Add((kvp.Key, async (s, onRead) => await Utils.CopyStreamAsync(cap.AsStream(), s, onRead, ct), size, kvp.Key));

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

                log?.Invoke($"- {label} {GetActionLog(originalExt, req.UseCompression, nca)}", LogLevel.Info, req.TargetBaseTitleId);
                fileEntries.Add((result.Value.FinalName, result.Value.Writer, result.Value.Size, result.Value.Label));
            }

            var meta = req.ResolvedMeta ?? ExtractFinalMetadata(keySet, allPaths, req.TargetBaseTitleId);

            log?.Invoke(string.Format(Res.Log_FinalId, meta.TitleId, meta.DisplayVersion), LogLevel.Ok, req.TargetBaseTitleId);

            string finalFileName = NameBuilder.FileNameBuild("Merged", meta.KrTitle, meta.EnTitle, meta.TitleId, meta.DisplayVersion, meta.TitleVersion, meta.DlcCount, hasBase, hasUpdate, req.UseCompression ? ContainerFormat.Nsz : ContainerFormat.Nsp);

            finalPath = Utils.GetUniqueFilePath(Path.Combine(req.OutputDir, finalFileName));

            while (allPaths.Any(p => string.Equals(p, finalPath, StringComparison.OrdinalIgnoreCase)) || File.Exists(finalPath))
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(finalPath);
                string ext = Path.GetExtension(finalPath);

                finalPath = Path.Combine(req.OutputDir, nameWithoutExt + "_" + ext);
            }

            string displayName = NameBuilder.DisplayNameBuild(meta.EnTitle, meta.TitleId, meta.DisplayVersion, meta.DlcCount, hasBase, hasUpdate, req.UseCompression ? ContainerFormat.Nsz : ContainerFormat.Nsp);
            using var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

            await Pfs0Builder.WriteAsync($"{Res.Log_Merging} {displayName}", meta.TitleId, fileEntries, fout, 0x20, progress, ct);

            if (req.UseCompression && converters.Count > 0 && isValidationEnabled)
            {
                log?.Invoke($"{req.TargetBaseTitleName} {Res.Log_ValidationStart} ({index}/{groupCount})", LogLevel.Highlight, req.TargetBaseTitleId);

                long totalValidationSize = converters.Keys
                    .Select(k => fileEntries.FirstOrDefault(f => string.Equals(f.Name, Path.ChangeExtension(k, ".ncz"), StringComparison.OrdinalIgnoreCase)).EstimatedSize)
                    .Sum();

                await RunValidation(fout, converters, totalValidationSize, req.TargetBaseTitleId, req.TargetBaseTitleName, progress, log, ct);
                log?.Invoke($"{req.TargetBaseTitleName} {Res.Log_ValidationComplete} ({index}/{groupCount})", LogLevel.Ok, req.TargetBaseTitleId);
            }

            isCompleted = true;

            log?.Invoke(string.Format($"{Res.Log_MergeComplete} ({index}/{groupCount})", finalFileName), LogLevel.Ok, req.TargetBaseTitleId);

            return finalPath;
        }
        catch (OperationCanceledException) 
        { 
            throw; 
        }
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

    private static string GetActionLog(string originalExt, bool useCompression, Nca nca)
    {
        bool canCompress = nca.Header.ContentType is NcaContentType.Program or NcaContentType.PublicData;

        if (originalExt == ".ncz")
            return useCompression && canCompress ? Res.Log_CompressAndMerge : Res.Log_DecompressAndMerge;

        return useCompression && canCompress ? Res.Log_CompressAndMerge : Res.Log_Merging;
    }
}