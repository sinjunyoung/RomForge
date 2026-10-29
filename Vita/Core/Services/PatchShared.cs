using Common;
using Patch.Core.Enums;
using Patch.Core.Services;
using System.IO.Compression;
using Vita.Core.Models;

namespace Vita.Core.Services;

internal static class PatchShared
{
    private const int ProgressChunkSize = 4 * 1024 * 1024;

    private static readonly string[] AppLayoutDirs = ["app", "rePatch"];

    private static readonly string[] AddcontLayoutDirs = ["addcont", "reAddcont"];

    private const int MaxLayoutWrapperDepth = 2;

    public static async Task<T> RunWithItemsAsync<T>(List<BatchSourceEntry> entries, Action<string, LogLevel> log, Func<List<SourceItem>, Task<T>> run)
    {
        var (items, ownedAccessors) = LoadItems(entries, log);

        try
        {
            return await run(items);
        }
        finally
        {
            foreach (var accessor in ownedAccessors)
                accessor.Dispose();
        }
    }

    public static (List<SourceItem> Items, List<ISourceAccessor> OwnedAccessors) LoadItems(List<BatchSourceEntry> entries, Action<string, LogLevel> log)
    {
        var ownedAccessors = new List<ISourceAccessor>();
        var items = new List<SourceItem>();
        var appSourceByTitle = new Dictionary<string, (ISourceAccessor Accessor, string SourcePath)>(StringComparer.OrdinalIgnoreCase);
        var orderedEntries = entries.OrderBy(GetEntrySortRank).ToList();

        void AddItem(ContentCategory? category, string titleId, string? contentIdSuffix, string sourcePath, ISourceAccessor accessor, string? patchPath)
        {
            items.Add(new SourceItem
            {
                Category = category,
                TitleId = titleId,
                ContentIdSuffix = contentIdSuffix,
                SourcePath = sourcePath,
                Accessor = accessor,
                PatchPathOverride = patchPath
            });

            if (category == ContentCategory.App)
                appSourceByTitle[titleId] = (accessor, sourcePath);
        }

        try
        {
            foreach (var entry in orderedEntries)
            {
                if (entry.Kind == SourceKind.Unpacked)
                    throw new NotSupportedException($"언팩된 폴더는 리팩(폴더 기반)에서만 사용할 수 있습니다: {entry.Path}");

                if (entry.Kind == SourceKind.Pkg)
                {
                    if (entry.Probe is null)
                        throw new InvalidOperationException($"PKG 항목에 Probe 정보가 없습니다: {entry.Path}");

                    ISourceAccessor accessor;

                    if (entry.Probe.Category == ContentCategory.Patch && string.IsNullOrWhiteSpace(entry.License) && appSourceByTitle.TryGetValue(entry.Probe.TitleId!, out var appSource))
                    {
                        string appWorkBinRel = $"{appSource.SourcePath}/sce_sys/package/work.bin";
                        var appLicense = WorkBinReader.Read(appSource.Accessor, appWorkBinRel);

                        accessor = new PkgSourceAccessor(entry.Path, appLicense.Klicensee);

                        log($"[patch] {entry.Probe.TitleId}: app의 라이선스를 그대로 공유해서 적용함", LogLevel.Info);
                    }
                    else
                        accessor = new PkgSourceAccessor(entry.Path, entry.License);

                    ownedAccessors.Add(accessor);
                    AddItem(entry.Probe.Category, entry.Probe.TitleId!, entry.Probe.ContentIdSuffix, string.Empty, accessor, entry.PatchPath);
                }
                else
                {
                    var accessor = SourceAccessorFactory.Open(entry.Path!);

                    ownedAccessors.Add(accessor);

                    if (entry.ItemSourcePath != null)
                    {
                        var itemCategory = entry.ItemCategory ?? throw new InvalidOperationException($"항목 카테고리가 없습니다: {entry.Path}");
                        var itemTitleId = entry.ItemTitleId ?? throw new InvalidOperationException($"항목 TitleId가 없습니다: {entry.Path}");

                        AddItem(itemCategory, itemTitleId, entry.ItemContentIdSuffix, entry.ItemSourcePath, accessor, entry.PatchPath);
                    }
                    else
                    {
                        foreach (var discovered in SourcePreparer.DiscoverItems(accessor))
                            AddItem(discovered.Category, discovered.TitleId!, discovered.ContentIdSuffix, discovered.SourcePath!, accessor, entry.PatchPath);
                    }
                }
            }
        }
        catch
        {
            foreach (var owned in ownedAccessors)
                owned.Dispose();
            throw;
        }

        return (items, ownedAccessors);
    }

    public static List<MergeGroup> BuildGroups(List<SourceItem> items, string defaultPatchPath, Dictionary<string, PatchContext> patchContexts, OutputTarget target, Action<string, LogLevel> log, CancellationToken ct)
        => [.. BuildBaseGroups(items, log, ct).Select(g => CreateGroup(g.Category, g.TitleId, g.ContentIdSuffix, g.PatchPathOverride ?? defaultPatchPath, g.Index, g.Owners, patchContexts, target))];

    internal static List<BaseGroup> BuildBaseGroups(List<SourceItem> items, Action<string, LogLevel> log, CancellationToken ct)
    {
        var appByTitle = items.Where(i => i.Category == ContentCategory.App).ToDictionary(i => i.TitleId, StringComparer.OrdinalIgnoreCase);
        var patchByTitle = items.Where(i => i.Category == ContentCategory.Patch).ToDictionary(i => i.TitleId, StringComparer.OrdinalIgnoreCase);
        var addcontItems = items.Where(i => i.Category == ContentCategory.Addcont).ToList();
        var titleIds = appByTitle.Keys.Union(patchByTitle.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        var groups = new List<BaseGroup>();

        foreach (var titleId in titleIds)
        {
            ct.ThrowIfCancellationRequested();
            appByTitle.TryGetValue(titleId, out var appItem);
            patchByTitle.TryGetValue(titleId, out var patchItem);

            (ISourceAccessor Accessor, string Rel)? appWorkBinFallback = appItem?.Accessor != null ? (appItem.Accessor, $"{appItem.SourcePath}/sce_sys/package/work.bin") : null;
            var appOwner = appItem != null ? BuildOwnerContext(appItem, null, log) : null;
            var patchOwner = patchItem != null ? BuildOwnerContext(patchItem, appWorkBinFallback, log) : null;
            var owners = new List<OwnerContext>();

            if (appOwner != null)
                owners.Add(appOwner);

            if (patchOwner != null)
                owners.Add(patchOwner);

            if (owners.Count == 0)
                continue;

            groups.Add(new BaseGroup
            {
                Category = ContentCategory.App,
                TitleId = titleId,
                PatchPathOverride = patchItem?.PatchPathOverride ?? appItem?.PatchPathOverride,
                Index = BuildPatchAppIndex(appOwner, patchOwner),
                Owners = owners
            });
        }

        foreach (var addcontItem in addcontItems)
        {
            ct.ThrowIfCancellationRequested();

            var owner = BuildOwnerContext(addcontItem, null, log);

            if (owner is null)
                continue;

            groups.Add(new BaseGroup
            {
                Category = ContentCategory.Addcont,
                TitleId = addcontItem.TitleId,
                ContentIdSuffix = addcontItem.ContentIdSuffix,
                PatchPathOverride = addcontItem.PatchPathOverride,
                Index = BuildPatchAppIndex(owner, null),
                Owners = [owner]
            });
        }

        return groups;
    }

    private static MergeGroup CreateGroup(ContentCategory category, string titleId, string? contentIdSuffix, string patchPackagePath, Dictionary<string, PatchAppEntry> index, List<OwnerContext> owners, Dictionary<string, PatchContext> patchContexts, OutputTarget target)
    {
        var patchCtx = GetPatchContext(patchContexts, patchPackagePath);
        var targets = BuildTargets(index, patchCtx, category, titleId, contentIdSuffix, target);
        return new MergeGroup { Category = category, TitleId = titleId, ContentIdSuffix = contentIdSuffix, PatchCtx = patchCtx, Index = index, Targets = targets, Owners = owners };
    }

    public static string BuildEntryPath(string prefix, MergeGroup group, string relativePath) => BuildEntryPath(prefix, group.Category, group.TitleId, group.ContentIdSuffix, relativePath);

    public static string BuildEntryPath(string prefix, ContentCategory? category, string? titleId, string? contentIdSuffix, string relativePath)
        => category == ContentCategory.Addcont ? ToSafeEntryPath($"{prefix}/{titleId}/{contentIdSuffix}/{relativePath}") : ToSafeEntryPath($"{prefix}/{titleId}/{relativePath}");

    private static string ToSafeEntryPath(string path)
    {
        string normalized = NormalizeZipPath(path);

        if (normalized.Split('/').Contains(".."))
            throw new InvalidDataException($"허용되지 않는 경로입니다: {path}");

        return normalized;
    }

    private static int GetEntrySortRank(BatchSourceEntry entry)
    {
        var category = entry.Kind == SourceKind.Pkg ? entry.Probe?.Category : entry.ItemCategory;

        return category switch
        {
            ContentCategory.App => 0,
            ContentCategory.Patch => 1,
            ContentCategory.Addcont => 2,
            _ => 3
        };
    }

    public static PatchContext GetPatchContext(Dictionary<string, PatchContext> cache, string patchPath)
    {
        if (cache.TryGetValue(patchPath, out var existing))
            return existing;

        var accessor = SourceAccessorFactory.Open(patchPath);
        var ctx = new PatchContext { Accessor = accessor, Files = [.. accessor.EnumerateAllFiles().Select(raw => new PatchContainerFile(NormalizeZipPath(raw), raw))] };

        cache[patchPath] = ctx;

        return ctx;
    }

    private static PatchFileIndex BuildPatchIndex(PatchContext ctx, ContentCategory category, string titleId, string? contentIdSuffix, OutputTarget target)
    {
        bool isAddcont = category == ContentCategory.Addcont;
        string? layoutRoot = FindLayoutRoot(ctx.Files, GetLayoutDirs(isAddcont, target), titleId, isAddcont ? contentIdSuffix : null);
        var rawByRelative = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (layoutRoot != null)
        {
            foreach (var file in ctx.Files)
            {
                if (file.Path.StartsWith(layoutRoot, StringComparison.OrdinalIgnoreCase))
                    rawByRelative.TryAdd(file.Path[layoutRoot.Length..], file.Raw);
            }
        }

        if (!isAddcont)
        {
            foreach (var file in ctx.Files)
            {
                if (!IsLayoutPath(file.Path))
                    rawByRelative.TryAdd(file.Path, file.Raw);
            }
        }

        var index = PatchFileIndex.Build(rawByRelative.Keys, relative => CreatePatchFileRef(ctx.Accessor, rawByRelative[relative]));

        if (isAddcont)
        {
            var flatFiles = ctx.Files.Where(f => !IsLayoutPath(f.Path)).ToList();
            var rawByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in flatFiles)
                rawByPath.TryAdd(file.Path, file.Raw);

            foreach (var entry in PatchFileIndex.Build(rawByPath.Keys, path => CreatePatchFileRef(ctx.Accessor, rawByPath[path])).Entries.Where(e => e.Kind == PatchFileKind.BinaryPatch))
                index.Entries.Add(entry with { RelativeDir = PatchFileIndex.LooseOnlyDir });
        }

        return index;
    }

    private static string[] GetLayoutDirs(bool isAddcont, OutputTarget target)
    {
        string[] dirs = isAddcont ? AddcontLayoutDirs : AppLayoutDirs;
        return target == OutputTarget.Retail ? [dirs[1], dirs[0]] : dirs;
    }

    private static string? FindLayoutRoot(IReadOnlyList<PatchContainerFile> files, string[] layoutDirs, string titleId, string? contentIdSuffix)
    {
        string tail = string.IsNullOrEmpty(contentIdSuffix) ? titleId : $"{titleId}/{contentIdSuffix}";

        foreach (string layoutDir in layoutDirs)
        {
            string pattern = $"{layoutDir}/{tail}/";
            string? best = null;
            int bestDepth = int.MaxValue;

            foreach (var file in files)
            {
                for (int i = 0; i < file.Path.Length; i++)
                {
                    if (i > 0 && file.Path[i - 1] != '/')
                        continue;

                    if (!string.Equals(file.Path.AsSpan(i, Math.Min(pattern.Length, file.Path.Length - i)).ToString(), pattern, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int depth = file.Path.Take(i).Count(c => c == '/');

                    if (depth > MaxLayoutWrapperDepth)
                        break;

                    string candidate = file.Path[..(i + pattern.Length)];

                    if (depth < bestDepth || (depth == bestDepth && string.CompareOrdinal(candidate, best) < 0))
                    {
                        bestDepth = depth;
                        best = candidate;
                    }

                    break;
                }
            }

            if (best != null)
                return best;
        }

        return null;
    }

    private static bool IsLayoutPath(string path)
    {
        string[] segments = path.Split('/');

        for (int i = 0; i <= MaxLayoutWrapperDepth && i + 2 < segments.Length; i++)
        {
            if (AppLayoutDirs.Concat(AddcontLayoutDirs).Contains(segments[i], StringComparer.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static PatchFileRef CreatePatchFileRef(ISourceAccessor accessor, string rawPath) => PatchFileRef.FromStream(() => new MemoryStream(accessor.ReadAllBytes(rawPath), writable: false), accessor.GetFileSize(rawPath), Path.GetFileName(NormalizePath(rawPath)));

    public static (ISourceAccessor Accessor, string Rel)? ResolveWorkBin(ISourceAccessor source, SourceItem item, (ISourceAccessor Accessor, string Rel)? fallback)
    {
        string workBinRel = $"{item.SourcePath}/sce_sys/package/work.bin";

        if (source.FileExists(workBinRel))
            return (source, workBinRel);

        if (fallback is { } fb && fb.Accessor.FileExists(fb.Rel))
            return fb;

        return null;
    }

    public static string NormalizePath(string path) => path.Replace('\\', '/');

    public static string NormalizeZipPath(string path)
    {
        string normalized = NormalizePath(path);

        while (normalized.Contains("//"))
            normalized = normalized.Replace("//", "/");

        return normalized.Trim('/');
    }

    public static OwnerContext? BuildOwnerContext(SourceItem item, (ISourceAccessor Accessor, string Rel)? fallbackWorkBin, Action<string, LogLevel> log)
    {
        var accessor = item.Accessor ?? throw new InvalidOperationException("소스 accessor가 없습니다.");
        var workBin = ResolveWorkBin(accessor, item, fallbackWorkBin);

        if (workBin is null)
        {
            log($"{item.Category} {item.TitleId}: work.bin 없음, 건너뜀", LogLevel.Error);
            return null;
        }

        try
        {
            var license = WorkBinReader.Read(workBin.Value.Accessor, workBin.Value.Rel);
            var table = NoNpDrmDecryptor.ParseFileTable(accessor, item.SourcePath);
            return new OwnerContext { Item = item, Table = table, License = license, WorkBinAccessor = workBin.Value.Accessor, WorkBinRelativePath = workBin.Value.Rel };
        }
        catch (Exception ex)
        {
            log($"{item.Category} {item.TitleId}: 파일 목록 확인 실패 - {ex.Message}", LogLevel.Error);
            return null;
        }
    }

    public static List<(string RelativePath, long Size)> GetAllOwnedFiles(OwnerContext owner)
    {
        var sizeByPath = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in owner.Table.Entries)
        {
            if (entry.Type.IsDirectory())
                continue;

            string relativePath = NormalizePath(entry.RelativePath ?? entry.Name);

            sizeByPath[relativePath] = entry.Size;
        }

        string prefix = string.IsNullOrEmpty(owner.Item.SourcePath) ? string.Empty : NormalizePath(owner.Item.SourcePath).TrimEnd('/') + "/";

        foreach (var rawPath in owner.Item.Accessor!.EnumerateAllFiles())
        {
            string normalized = NormalizePath(rawPath);

            if (prefix.Length > 0)
            {
                if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                normalized = normalized[prefix.Length..];
            }

            if (!sizeByPath.ContainsKey(normalized))
                sizeByPath[normalized] = owner.Item.Accessor!.GetFileSize(rawPath);
        }

        return [.. sizeByPath.Select(kv => (kv.Key, kv.Value))];
    }

    public static Dictionary<string, PatchAppEntry> BuildPatchAppIndex(OwnerContext? appOwner, OwnerContext? patchOwner)
    {
        var index = new Dictionary<string, PatchAppEntry>(StringComparer.OrdinalIgnoreCase);

        if (appOwner != null)
            AddOwnerToIndex(index, appOwner);

        if (patchOwner != null)
            AddOwnerToIndex(index, patchOwner);

        return index;
    }

    private static void AddOwnerToIndex(Dictionary<string, PatchAppEntry> index, OwnerContext owner)
    {
        for (int i = 0; i < owner.Table.Entries.Count; i++)
        {
            var entry = owner.Table.Entries[i];

            if (entry.Type.IsDirectory())
                continue;

            string relativePath = NormalizePath(entry.RelativePath ?? entry.Name);

            index[relativePath] = new PatchAppEntry { Owner = owner, EntryIndex = i };
        }
    }

    internal static PatchPlan BuildPlan(PatchContext patchCtx, ContentCategory category, string titleId, string? contentIdSuffix, OutputTarget target, IEnumerable<string> relativePaths)
        => PatchPlan.Build(BuildPatchIndex(patchCtx, category, titleId, contentIdSuffix, target), relativePaths, StringComparer.OrdinalIgnoreCase);

    public static List<PatchTarget> BuildTargets(Dictionary<string, PatchAppEntry> patchAppIndex, PatchContext patchCtx, ContentCategory category, string titleId, string? contentIdSuffix, OutputTarget target)
    {
        var plan = BuildPlan(patchCtx, category, titleId, contentIdSuffix, target, patchAppIndex.Keys);
        var targets = new List<PatchTarget>();

        foreach (var (relativePath, appEntry) in patchAppIndex)
        {
            if (plan.TryGetOverwrite(relativePath, out var overwrite))
            {
                targets.Add(new PatchTarget
                {
                    RelativePath = relativePath,
                    Kind = PatchTargetKind.Overwrite,
                    Source = appEntry,
                    OverwriteFile = overwrite,
                    EstimatedSize = overwrite.Length
                });
            }
            else if (plan.IsPatched(relativePath))
            {
                targets.Add(new PatchTarget
                {
                    RelativePath = relativePath,
                    Kind = PatchTargetKind.Patch,
                    Source = appEntry,
                    Patches = plan.GetPatches(relativePath),
                    EstimatedSize = appEntry.FileEntry.Size
                });
            }
        }

        return targets;
    }

    public static async Task<byte[]?> ResolveTargetBytesAsync(PatchTarget target, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (target.Kind == PatchTargetKind.Overwrite)
            return await (target.OverwriteFile ?? throw new InvalidOperationException("교체 대상에 파일 정보가 없습니다.")).ReadSmallFileBytesAsync(ct);

        var appEntry = target.Source ?? throw new InvalidOperationException("패치 대상에 원본 정보가 없습니다.");
        var owner = appEntry.Owner;
        var entry = appEntry.FileEntry;
        byte[] sourceBytes = NoNpDrmDecryptor.DecryptEntry(owner.Item.Accessor!, owner.Item.SourcePath, owner.License.Klicensee, entry, owner.Table.UnicvEntries[appEntry.EntryIndex], owner.Table.FilesSalt);

        return await ApplyTargetAsync(target, sourceBytes, log, ct);
    }

    internal static async Task<byte[]?> ApplyTargetAsync(PatchTarget target, byte[] sourceBytes, Action<string, LogLevel> log, CancellationToken ct)
    {
        var (data, applied) = await PatchApplier.ApplyPatchesAsync(target.RelativePath, target.Patches, sourceBytes, log, ct);
        return applied ? data : null;
    }

    public static async Task WriteEntryWithProgressAsync(ZipArchiveEntry zipEntry, byte[] data, long estimatedSize, ProgressReporter reporter, CancellationToken ct)
    {
        using var entryStream = zipEntry.Open();

        await WriteStreamWithProgressAsync(entryStream, data, estimatedSize, reporter, ct);
    }

    public static async Task WriteStreamWithProgressAsync(Stream stream, byte[] data, long estimatedSize, ProgressReporter reporter, CancellationToken ct)
    {
        if (data.Length == 0)
        {
            reporter.AddProgress(estimatedSize);
            return;
        }

        int offset = 0;
        long reported = 0;

        while (offset < data.Length)
        {
            int size = Math.Min(ProgressChunkSize, data.Length - offset);

            await stream.WriteAsync(data.AsMemory(offset, size), ct);

            offset += size;

            long target = (long)((double)offset / data.Length * estimatedSize);

            if (target != reported)
                reporter.AddProgress(target - reported);

            reported = target;
        }
    }

    public static (string EntryPath, byte[] Data)? BuildLicenseEntry(OwnerContext owner)
    {
        if (!WorkBinReader.TryGetTitleIdFromContentId(owner.License.ContentId, out string licenseTitleId))
            return null;

        return (ToSafeEntryPath($"license/{licenseTitleId}/{owner.License.ContentId}.rif"), owner.WorkBinAccessor.ReadAllBytes(owner.WorkBinRelativePath));
    }

    public static void WriteLicenseEntry(ZipArchive zip, HashSet<string> writtenEntries, OwnerContext owner)
    {
        if (BuildLicenseEntry(owner) is not { } license)
            return;

        if (!writtenEntries.Add(license.EntryPath))
            return;

        var licenseEntry = zip.CreateEntry(license.EntryPath, CompressionLevel.NoCompression);
        using var es = licenseEntry.Open();

        es.Write(license.Data);
    }

    public static string GetPatchedPrefix(ContentCategory? category, OutputTarget target) => (category, target) switch
    {
        (ContentCategory.App, OutputTarget.Emu) => "app",
        (ContentCategory.Patch, OutputTarget.Emu) => "app",
        (ContentCategory.Addcont, OutputTarget.Emu) => "addcont",
        (ContentCategory.App, OutputTarget.Retail) => "rePatch",
        (ContentCategory.Patch, OutputTarget.Retail) => "rePatch",
        (ContentCategory.Addcont, OutputTarget.Retail) => "reAddcont",
        _ => throw new NotSupportedException()
    };

    public static string GetBasePrefix(ContentCategory? category) => category == ContentCategory.Addcont ? "addcont" : "app";

    public static string GetRetailBaseFolder(ContentCategory? category) => category switch
    {
        ContentCategory.App => "app",
        ContentCategory.Patch => "patch",
        ContentCategory.Addcont => "addcont",
        _ => throw new NotSupportedException()
    };
}