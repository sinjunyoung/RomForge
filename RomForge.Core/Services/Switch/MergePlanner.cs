using LibHac.Common.Keys;
using LibHac.Ncm;
using NSW.Core;
using NSW.Core.Models;
using RomForge.Core.Models.Switch;
using System.IO;

namespace RomForge.Core.Services.Switch;

internal static class MergePlanner
{
    public static Dictionary<string, TitleGroup> BuildTitleGroups(List<MetadataResult> allMeta)
    {
        var groups = new Dictionary<string, TitleGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var meta in allMeta)
        {
            string baseTid = LibHacHelper.GetBaseTitleId(meta.TitleId);

            if (string.IsNullOrEmpty(baseTid))
                continue;

            if (!groups.TryGetValue(baseTid, out var group))
            {
                group = new TitleGroup(baseTid, meta.KrTitle);
                groups[baseTid] = group;
            }

            switch (meta.Type)
            {
                case ContentMetaType.Application: group.BaseMetas.Add(meta);
                    break;
                case ContentMetaType.Patch: group.PatchMetas.Add(meta); 
                    break;
                case ContentMetaType.AddOnContent: group.DlcMetas.Add(meta);
                    break;
            }
        }

        return groups;
    }

    public static (Models.Switch.BuildRequest Request, bool HasBase, bool HasUpdate) BuildSingleRequest(List<MetadataResult> allMeta, IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode)
    {
        var bases = allMeta.Where(m => m.Type == ContentMetaType.Application).ToList();
        var patches = allMeta.Where(m => m.Type == ContentMetaType.Patch).ToList();
        var dlcs = allMeta.Where(m => m.Type == ContentMetaType.AddOnContent).ToList();
        var baseMeta = bases.FirstOrDefault() ?? patches.OrderByDescending(m => m.TitleVersion).FirstOrDefault() ?? dlcs.FirstOrDefault() ?? allMeta[0];
        string baseTitleId = LibHacHelper.GetBaseTitleId(baseMeta.TitleId);
        var latestPatch = patches
            .Where(m => string.Equals(LibHacHelper.GetBaseTitleId(m.TitleId), baseTitleId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.TitleVersion)
            .FirstOrDefault();
        var versionMeta = latestPatch ?? baseMeta;
        var latestPatches = patches
            .GroupBy(m => LibHacHelper.GetBaseTitleId(m.TitleId), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(m => m.TitleVersion).First())
            .ToList();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var meta in bases.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        foreach (var meta in latestPatches)
            AddNcaIds(allowed, meta);

        foreach (var meta in dlcs.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        var sources = inputPaths
            .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var req = new Models.Switch.BuildRequest(string.Empty, string.Empty, [], outputDir)
        {
            UseCompression = useCompression,
            CompressionLevel = compressionLevel,
            UseBlockMode = useBlockMode,
            AllSourcePaths = sources,
            TargetBaseTitleId = baseTitleId,
            TargetBaseTitleName = baseMeta.KrTitle,
            AllowedNcaIds = allowed.Count > 0 ? allowed : null,
            ResolvedMeta = new MetadataResult(
                baseMeta.TitleId, versionMeta.TitleVersion, versionMeta.DisplayVersion, baseMeta.KrTitle, baseMeta.EnTitle,
                dlcs.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Count(), Type: bases.Count > 0 ? ContentMetaType.Application : baseMeta.Type),
        };

        return (req, bases.Count > 0, patches.Count > 0);
    }

    public static HashSet<string>? BuildAllowedNcaIds(TitleGroup group, MetadataResult? latestPatch)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var meta in group.BaseMetas.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        if (latestPatch != null)
            AddNcaIds(allowed, latestPatch);

        foreach (var meta in group.DlcMetas.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        return allowed.Count > 0 ? allowed : null;
    }

    private static void AddNcaIds(HashSet<string> set, MetadataResult meta)
    {
        if (meta.ContentNcaIds == null)
            return;

        foreach (var id in meta.ContentNcaIds) 
            set.Add(id);
    }

    public static List<string> GetAllPaths(Models.Switch.BuildRequest req)
    {
        if (req.AllSourcePaths is { Count: > 0 })
            return [.. req.AllSourcePaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))];

        var list = new List<string>();

        if (!string.IsNullOrEmpty(req.UpdateFilePath) && File.Exists(req.UpdateFilePath))
            list.Add(req.UpdateFilePath);

        foreach (var p in req.DlcFilePaths)
            if (!string.IsNullOrEmpty(p) && File.Exists(p) && !list.Contains(p))
                list.Add(p);

        if (!string.IsNullOrEmpty(req.BaseFilePath) && !list.Contains(req.BaseFilePath))
            list.Add(req.BaseFilePath);

        return list;
    }

    public static MetadataResult ExtractFinalMetadata(KeySet ks, List<string> paths, string? targetBaseTitleId = null)
    {
        var allMetas = paths
            .SelectMany(p => MetadataReader.GetMetadataFromContainer(ks, p))
            .GroupBy(m => new { m.TitleId, m.TitleVersion, m.Type })
            .Select(g => g.First())
            .ToList();

        if (!string.IsNullOrEmpty(targetBaseTitleId))
        {
            allMetas = [.. allMetas.Where(m =>
            {
                string baseTid = LibHacHelper.GetBaseTitleId(m.TitleId);

                if (string.IsNullOrEmpty(baseTid))
                    return false;

                return baseTid.Equals(targetBaseTitleId, StringComparison.OrdinalIgnoreCase);
            })];
        }

        if (allMetas.Count == 0)
            return new MetadataResult(string.Empty, 0, "1.0.0", string.Empty, string.Empty, 0, ContentMetaType.Application);

        int dlcCount = allMetas.Count(m => m.Type == ContentMetaType.AddOnContent);
        var latestPatch = allMetas.Where(m => m.Type == ContentMetaType.Patch).OrderByDescending(m => m.TitleVersion).FirstOrDefault();
        var baseGame = allMetas.FirstOrDefault(m => m.Type == ContentMetaType.Application) ?? allMetas.First();
        var versionSource = latestPatch ?? baseGame;

        return new MetadataResult(baseGame.TitleId, versionSource.TitleVersion, versionSource.DisplayVersion, baseGame.KrTitle, baseGame.EnTitle, dlcCount, ContentMetaType.Application);
    }
}