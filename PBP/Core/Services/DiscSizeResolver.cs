namespace PBP.Core.Services;

public static class DiscSizeResolver
{
    public static long GetTotalSize(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ext switch
        {
            ".cue" => CueFileResolver.GetAllReferencedFiles(path)
                .Where(File.Exists)
                .Sum(f => new FileInfo(f).Length),
            ".m3u" => GetM3uTotalSize(path),
            _ => File.Exists(path) ? new FileInfo(path).Length : 0
        };
    }

    private static long GetM3uTotalSize(string m3uPath)
    {
        var dir = Path.GetDirectoryName(m3uPath)!;
        long total = 0;

        foreach (var line in File.ReadAllLines(m3uPath))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#')) 
                continue;

            var fullPath = Path.IsPathRooted(trimmed) ? trimmed : Path.Combine(dir, trimmed);

            total += GetTotalSize(fullPath);
        }

        return total;
    }
}