using Patch.Core.Services;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.IO;

namespace RomForge.Core.Services.Util;

public static class TistoryArchiveExtractor
{
    private static readonly string[] SupportedExtensions = [".zip", ".7z", ".rar"];

    public static bool IsArchiveFile(string? path) => !string.IsNullOrEmpty(path) && SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static string ExtractAndDeleteSource(string archivePath)
    {
        string dir = Path.GetDirectoryName(archivePath) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(archivePath);
        string extractDir = GetUniqueDirectory(Path.Combine(dir, name));

        Directory.CreateDirectory(extractDir);

        if (string.Equals(Path.GetExtension(archivePath), ".7z", StringComparison.OrdinalIgnoreCase))
            ExtractSevenZip(archivePath, extractDir);
        else
            ExtractSharpCompress(archivePath, extractDir);

        File.Delete(archivePath);

        return extractDir;
    }

    private static void ExtractSevenZip(string archivePath, string extractDir)
    {
        NativeSevenZip.EnsureInitialized();

        using var extractor = new SevenZipExtractor(archivePath);

        extractor.ExtractArchive(extractDir);
    }

    private static void ExtractSharpCompress(string archivePath, string extractDir)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        var options = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            entry.WriteToDirectory(extractDir, options);
    }

    private static string GetUniqueDirectory(string path)
    {
        if (!Directory.Exists(path))
            return path;

        int index = 1;
        string candidate;

        do
        {
            candidate = $"{path} ({index})";
            index++;
        }
        while (Directory.Exists(candidate));

        return candidate;
    }
}