namespace Patch.Core.Services;

public static class PatchFolderResolver
{
    public static string? FindSubDir(string? patchRoot, string folderName)
    {
        if (string.IsNullOrEmpty(patchRoot) || !Directory.Exists(patchRoot))
            return null;

        string direct = Path.Combine(patchRoot, folderName);

        if (Directory.Exists(direct))
            return direct;

        var queue = new Queue<string>();

        queue.Enqueue(patchRoot);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            string[] subDirs;

            try { subDirs = Directory.GetDirectories(current); }
            catch { continue; }

            foreach (string dir in subDirs)
            {
                if (string.Equals(Path.GetFileName(dir), folderName, StringComparison.OrdinalIgnoreCase))
                    return dir;

                queue.Enqueue(dir);
            }
        }

        return null;
    }

    public static string? FindAncestorSubDir(string? startPath, string folderName, int maxLevelsUp = 8)
    {
        if (string.IsNullOrEmpty(startPath))
            return null;

        var dir = Directory.Exists(startPath) ? new DirectoryInfo(startPath) : new DirectoryInfo(startPath).Parent;

        for (int i = 0; i < maxLevelsUp && dir != null; i++)
        {
            string candidate = Path.Combine(dir.FullName, folderName);

            if (Directory.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }

    public static string? FindPatchRoot(string? patchRoot, params string[] anchorNames)
    {
        if (string.IsNullOrEmpty(patchRoot) || !Directory.Exists(patchRoot))
            return null;

        bool HasAnchor(string dir) => anchorNames.Any(name => Directory.Exists(Path.Combine(dir, name)));

        if (HasAnchor(patchRoot))
            return patchRoot;

        var queue = new Queue<string>();

        queue.Enqueue(patchRoot);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            string[] subDirs;

            try { subDirs = Directory.GetDirectories(current); }
            catch { continue; }

            foreach (string dir in subDirs)
            {
                if (HasAnchor(dir))
                    return dir;

                queue.Enqueue(dir);
            }
        }

        return null;
    }
}