using Patch.Core.Services;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.IO;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.Util;

public static class TistoryArchiveExtractor
{
    private static readonly string[] SupportedExtensions = [".zip", ".7z", ".rar"];

    private static readonly Regex SevenZipVolumeRegex = new(@"^(?<base>.+\.7z)\.(?<part>\d{3,})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RarVolumeRegex = new(@"^(?<base>.+)\.part(?<part>\d+)\.rar$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsArchiveFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            return true;

        return IsVolumePart(Path.GetFileName(path));
    }

    public static bool IsVolumePart(string fileName) => SevenZipVolumeRegex.IsMatch(fileName) || RarVolumeRegex.IsMatch(fileName);

    public static bool IsFirstVolumePart(string fileName)
    {
        var sevenZipMatch = SevenZipVolumeRegex.Match(fileName);

        if (sevenZipMatch.Success)
            return int.Parse(sevenZipMatch.Groups["part"].Value) == 1;

        var rarMatch = RarVolumeRegex.Match(fileName);

        if (rarMatch.Success)
            return int.Parse(rarMatch.Groups["part"].Value) == 1;

        return true;
    }

    public static string GetGroupKey(string fileName)
    {
        var sevenZipMatch = SevenZipVolumeRegex.Match(fileName);

        if (sevenZipMatch.Success)
            return sevenZipMatch.Groups["base"].Value.ToLowerInvariant();

        var rarMatch = RarVolumeRegex.Match(fileName);

        if (rarMatch.Success)
            return (rarMatch.Groups["base"].Value + ".rar").ToLowerInvariant();

        return fileName.ToLowerInvariant();
    }

    public static bool HasContiguousParts(IEnumerable<string> fileNames)
    {
        var parts = fileNames.Select(GetPartNumber).Where(p => p > 0).OrderBy(p => p).ToList();

        if (parts.Count == 0)
            return true;

        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] != i + 1)
                return false;
        }

        return true;
    }

    public static string ExtractAndDeleteSource(string firstPartPath, IReadOnlyList<string> allPartPaths, IProgress<int>? progress = null)
    {
        string dir = Path.GetDirectoryName(firstPartPath) ?? string.Empty;
        string displayName = GetDisplayName(Path.GetFileName(firstPartPath));
        string extractDir = GetUniqueDirectory(Path.Combine(dir, displayName));

        Directory.CreateDirectory(extractDir);

        if (IsSevenZipFormat(Path.GetFileName(firstPartPath)))
            ExtractSevenZip(firstPartPath, extractDir, progress);
        else
            ExtractSharpCompress(firstPartPath, extractDir, progress);

        foreach (var path in allPartPaths)
            File.Delete(path);

        return extractDir;
    }

    private static int GetPartNumber(string fileName)
    {
        var sevenZipMatch = SevenZipVolumeRegex.Match(fileName);

        if (sevenZipMatch.Success)
            return int.Parse(sevenZipMatch.Groups["part"].Value);

        var rarMatch = RarVolumeRegex.Match(fileName);

        if (rarMatch.Success)
            return int.Parse(rarMatch.Groups["part"].Value);

        return 0;
    }

    private static bool IsSevenZipFormat(string fileName) => string.Equals(Path.GetExtension(fileName), ".7z", StringComparison.OrdinalIgnoreCase) || SevenZipVolumeRegex.IsMatch(fileName);

    private static string GetDisplayName(string fileName)
    {
        var sevenZipMatch = SevenZipVolumeRegex.Match(fileName);

        if (sevenZipMatch.Success)
            return Path.GetFileNameWithoutExtension(sevenZipMatch.Groups["base"].Value);

        var rarMatch = RarVolumeRegex.Match(fileName);

        if (rarMatch.Success)
            return rarMatch.Groups["base"].Value;

        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static void ExtractSevenZip(string archivePath, string extractDir, IProgress<int>? progress)
    {
        NativeSevenZip.EnsureInitialized();

        using var extractor = new SevenZipExtractor(archivePath);

        if (progress != null)
            extractor.Extracting += (s, e) => progress.Report(e.PercentDone);

        extractor.ExtractArchive(extractDir);

        progress?.Report(100);
    }

    private static void ExtractSharpCompress(string archivePath, string extractDir, IProgress<int>? progress)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);

        var options = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        long totalSize = entries.Sum(e => e.Size);
        long extractedSize = 0;

        foreach (var entry in entries)
        {
            entry.WriteToDirectory(extractDir, options);

            extractedSize += entry.Size;

            if (totalSize > 0)
                progress?.Report((int)(extractedSize * 100 / totalSize));
        }

        progress?.Report(100);
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