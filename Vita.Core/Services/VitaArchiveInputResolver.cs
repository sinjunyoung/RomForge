using SharpCompress.Archives;
using SharpCompress.Common;

namespace Vita.Core.Services;

public static class VitaArchiveInputResolver
{
    public static string Resolve(string sourcePath, string workDir)
    {
        if (Directory.Exists(sourcePath))
            return sourcePath;

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("소스 경로를 찾을 수 없습니다.", sourcePath);

        string ext = Path.GetExtension(sourcePath);

        if (!string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"지원하지 않는 소스 형식입니다: {ext} (폴더 / zip만 지원)");

        Directory.CreateDirectory(workDir);
        ExtractWithSharpCompress(sourcePath, workDir);

        return workDir;
    }

    private static void ExtractWithSharpCompress(string archivePath, string destDir)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);

        foreach (var entry in archive.Entries)
        {
            if (entry.IsDirectory)
                continue;

            entry.WriteToDirectory(destDir, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
    }
}