using System.IO.Compression;

namespace Vita.Core.Services;

public static class VitaOutputZipper
{
    public static void ZipAndCleanup(string sourceDir, string zipPath)
    {
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        ZipFile.CreateFromDirectory(sourceDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        Directory.Delete(sourceDir, recursive: true);
    }
}