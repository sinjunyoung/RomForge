using Vita.Core.Models;

namespace Vita.Core.Services;

public static class SourcePreparer
{
    private const int MaxSearchDepth = 8;

    public static List<SourceItem> DiscoverItems(ISourceAccessor accessor)
    {
        var contentRoots = new List<string>();

        FindContentRoots(accessor, string.Empty, contentRoots, 0);

        var items = new List<SourceItem>();

        foreach (var root in contentRoots)
        {
            var item = ClassifyContentRoot(accessor, root);

            if (item != null)
                items.Add(item);
        }

        return items;
    }

    private static void FindContentRoots(ISourceAccessor accessor, string path, List<string> results, int depth)
    {
        if (depth > MaxSearchDepth)
            return;

        if (IsContentRoot(accessor, path))
        {
            results.Add(path);
            return;
        }

        foreach (var name in accessor.EnumerateDirectoryNames(path))
        {
            string childPath = string.IsNullOrEmpty(path) ? name : $"{path}/{name}";

            FindContentRoots(accessor, childPath, results, depth + 1);
        }
    }

    private static bool IsContentRoot(ISourceAccessor accessor, string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return accessor.DirectoryExists($"{path}/sce_pfs") && accessor.DirectoryExists($"{path}/sce_sys");
    }

    private static SourceItem? ClassifyContentRoot(ISourceAccessor accessor, string contentRootPath)
    {
        string sfoPath = $"{contentRootPath}/sce_sys/param.sfo";
        Dictionary<string, byte[]>? sfo = null;

        if (accessor.FileExists(sfoPath))
        {
            try
            {
                sfo = SfoParser.Parse(accessor.ReadAllBytes(sfoPath));
            }
            catch { }
        }

        string? contentId = sfo != null ? SfoParser.GetString(sfo, "CONTENT_ID") : null;
        string? titleIdFromContentId = null;

        if (contentId != null && WorkBinReader.TryGetTitleIdFromContentId(contentId, out string parsedTitleId))
            titleIdFromContentId = parsedTitleId;

        var segments = contentRootPath.Split('/');
        string ownName = segments[^1];
        string? parentName = segments.Length >= 2 ? segments[^2] : null;
        bool hasPatchAncestor = segments.Any(s => string.Equals(s, "patch", StringComparison.OrdinalIgnoreCase));
        bool hasAddcontAncestor = segments.Any(s => string.Equals(s, "addcont", StringComparison.OrdinalIgnoreCase));
        bool looksLikeAddcont = hasAddcontAncestor || (titleIdFromContentId != null && parentName != null && string.Equals(parentName, titleIdFromContentId, StringComparison.OrdinalIgnoreCase));

        if (looksLikeAddcont)
        {
            string titleId = titleIdFromContentId ?? parentName ?? ownName;
            string contentIdSuffix = contentId != null && contentId.Length > 20 ? contentId[20..] : ownName;

            return new SourceItem
            {
                Category = ContentCategory.Addcont,
                TitleId = titleId,
                ContentIdSuffix = contentIdSuffix,
                SourcePath = contentRootPath
            };
        }

        string resolvedTitleId = titleIdFromContentId ?? ownName;
        string? category = sfo != null ? SfoParser.GetString(sfo, "CATEGORY") : null;
        bool isPatch = hasPatchAncestor || string.Equals(category, "gp", StringComparison.OrdinalIgnoreCase);

        return new SourceItem
        {
            Category = isPatch ? ContentCategory.Patch : ContentCategory.App,
            TitleId = resolvedTitleId,
            SourcePath = contentRootPath
        };
    }
}