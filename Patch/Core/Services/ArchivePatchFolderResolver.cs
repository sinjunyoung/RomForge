namespace Patch.Core.Services;

public static class ArchivePatchFolderResolver
{
    public static string? FindSubDir(IReadOnlyList<string> entryPaths, string folderName)
    {
        string? best = null;
        int bestDepth = int.MaxValue;

        foreach (string dir in CollectDirs(entryPaths))
        {
            if (dir.Length == 0)
                continue;

            int slash = dir.LastIndexOf('/');
            string name = slash < 0 ? dir : dir[(slash + 1)..];

            if (!string.Equals(name, folderName, StringComparison.OrdinalIgnoreCase))
                continue;

            int depth = dir.Count(c => c == '/');

            if (depth < bestDepth)
            {
                bestDepth = depth;
                best = dir + "/";
            }
        }

        return best;
    }

    public static string? FindPatchRoot(IReadOnlyList<string> entryPaths, params string[] anchorNames)
    {
        var allDirs = CollectDirs(entryPaths);
        string? best = null;
        int bestDepth = int.MaxValue;

        foreach (string dir in allDirs)
        {
            bool hasAnchor = anchorNames.Any(a => allDirs.Contains(dir.Length == 0 ? a : $"{dir}/{a}"));

            if (!hasAnchor)
                continue;

            int depth = dir.Length == 0 ? 0 : dir.Count(c => c == '/') + 1;

            if (depth < bestDepth)
            {
                bestDepth = depth;
                best = dir.Length == 0 ? string.Empty : dir + "/";
            }
        }

        return best;
    }

    private static HashSet<string> CollectDirs(IReadOnlyList<string> entryPaths)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };

        foreach (string path in entryPaths)
        {
            int idx = path.LastIndexOf('/');
            string dir = idx < 0 ? string.Empty : path[..idx];

            while (true)
            {
                dirs.Add(dir);

                if (dir.Length == 0)
                    break;

                int i = dir.LastIndexOf('/');
                dir = i < 0 ? string.Empty : dir[..i];
            }
        }

        return dirs;
    }
}