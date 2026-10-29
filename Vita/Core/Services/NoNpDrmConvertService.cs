using Common;
using System.IO.Compression;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class NoNpDrmConvertService
{
    public static Task<List<string>> ConvertAsync(List<BatchSourceEntry> entries, string outputRoot, PkgConvertOutput output, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => Task.Run(() => ConvertCore(entries, outputRoot, output, log, progress, ct), ct);

    private static List<string> ConvertCore(List<BatchSourceEntry> entries, string outputRoot, PkgConvertOutput output, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var groups = BuildGroups(entries);

        Validate(groups);

        Directory.CreateDirectory(outputRoot);

        long totalBytes = entries.Sum(e => new FileInfo(e.Path!).Length);
        var reporter = new ProgressReporter("변환 중", string.Empty, totalBytes, progress);
        var outputs = new List<string>();

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            string outputPath = output == PkgConvertOutput.Zip ? Utils.GetUniqueFilePath(Path.Combine(outputRoot, SanitizeFileName(group.TitleId) + ".zip")) : GetUniqueDirectoryPath(Path.Combine(outputRoot, SanitizeFileName(group.TitleId)));

            try
            {
                ConvertGroup(group, outputPath, output, reporter, log, ct);
            }
            catch
            {
                DeleteQuietly(outputPath);
                throw;
            }

            outputs.Add(outputPath);

            log($"[{group.TitleId}] PKG {group.Entries.Count}개 -> {outputPath}", LogLevel.Ok);
        }

        reporter.ForceReport();

        return outputs;
    }

    private sealed record TitleGroup(string? TitleId, List<BatchSourceEntry> Entries);

    private static List<TitleGroup> BuildGroups(List<BatchSourceEntry> entries)
    {
        if (entries.Any(e => e.Kind != SourceKind.Pkg || e.Probe is null))
            throw new InvalidOperationException("PKG 파일만 변환할 수 있습니다.");

        return [.. entries
            .GroupBy(e => e.Probe!.TitleId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TitleGroup(g.First().Probe?.TitleId, [.. g.OrderBy(e => GetRank(e.Probe?.Category)).ThenBy(e => e.Probe?.ContentIdSuffix, StringComparer.OrdinalIgnoreCase)]))
            .OrderBy(g => g.TitleId, StringComparer.OrdinalIgnoreCase)];
    }

    private static int GetRank(ContentCategory? category) => category switch
    {
        ContentCategory.App => 0,
        ContentCategory.Patch => 1,
        _ => 2
    };

    private static void Validate(List<TitleGroup> groups)
    {
        foreach (var group in groups)
        {
            var duplicate = group.Entries
                .GroupBy(e => $"{e.Probe!.Category}/{e.Probe.ContentIdSuffix}", StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
                throw new InvalidOperationException($"[{group.TitleId}] 같은 종류의 PKG가 중복되었습니다: {string.Join(", ", duplicate.Select(e => Path.GetFileName(e.Path)))}");

            bool appHasLicense = group.Entries.Any(e => e.Probe!.Category == ContentCategory.App && !string.IsNullOrWhiteSpace(e.License));

            foreach (var entry in group.Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.License))
                    continue;

                if (entry.Probe!.Category == ContentCategory.Patch && appHasLicense)
                    continue;

                throw new InvalidOperationException($"[{group.TitleId}] 키(work.bin, *.rif / zRIF)가 필요합니다: {Path.GetFileName(entry.Path)}");
            }
        }
    }

    private static void ConvertGroup(TitleGroup group, string outputPath, PkgConvertOutput output, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        var accessors = new List<(BatchSourceEntry Entry, PkgSourceAccessor Accessor)>();

        try
        {
            byte[]? appKlicensee = null;

            foreach (var entry in group.Entries)
            {
                PkgSourceAccessor accessor;

                if (string.IsNullOrWhiteSpace(entry.License))
                {
                    accessor = new PkgSourceAccessor(entry.Path, appKlicensee ?? throw new InvalidOperationException($"[{group.TitleId}] App의 키를 찾을 수 없습니다: {Path.GetFileName(entry.Path)}"));

                    log($"[{group.TitleId}] {Path.GetFileName(entry.Path)}: App의 키를 공유해서 변환합니다", LogLevel.Info);
                }
                else
                    accessor = new PkgSourceAccessor(entry.Path, entry.License);

                accessors.Add((entry, accessor));

                if (entry.Probe!.Category == ContentCategory.App && accessor.FileExists("sce_sys/package/work.bin"))
                    appKlicensee = WorkBinReader.Read(accessor, "sce_sys/package/work.bin").Klicensee;
            }

            if (output == PkgConvertOutput.Zip)
                WriteZip(group, accessors, outputPath, reporter, ct);
            else
                WriteFolder(group, accessors, outputPath, reporter, ct);
        }
        finally
        {
            foreach (var (_, accessor) in accessors)
                accessor.Dispose();
        }
    }

    private static void WriteZip(TitleGroup group, List<(BatchSourceEntry Entry, PkgSourceAccessor Accessor)> accessors, string zipPath, ProgressReporter reporter, CancellationToken ct)
    {
        using var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Create);

        foreach (var (entry, accessor) in accessors)
        {
            foreach (string rel in accessor.EnumerateAllFiles())
            {
                ct.ThrowIfCancellationRequested();

                var zipEntry = zip.CreateEntry(BuildEntryPath(group, entry, rel), CompressionLevel.NoCompression);
                using var entryStream = zipEntry.Open();

                accessor.CopyTo(rel, entryStream, reporter.AddProgress);
            }
        }
    }

    private static void WriteFolder(TitleGroup group, List<(BatchSourceEntry Entry, PkgSourceAccessor Accessor)> accessors, string folderPath, ProgressReporter reporter, CancellationToken ct)
    {
        string root = Path.GetFullPath(folderPath) + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(folderPath);

        foreach (var (entry, accessor) in accessors)
        {
            foreach (string rel in accessor.EnumerateAllFiles())
            {
                ct.ThrowIfCancellationRequested();

                string fullPath = Path.GetFullPath(Path.Combine(folderPath, BuildEntryPath(group, entry, rel).Replace('/', Path.DirectorySeparatorChar)));

                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"허용되지 않는 경로입니다: {rel}");

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

                accessor.CopyTo(rel, fileStream, reporter.AddProgress);
            }
        }
    }

    private static string BuildEntryPath(TitleGroup group, BatchSourceEntry entry, string relativePath)
    {
        var probe = entry.Probe!;
        return PatchShared.BuildEntryPath(PatchShared.GetRetailBaseFolder(probe.Category), probe.Category, group.TitleId, probe.ContentIdSuffix, relativePath);
    }

    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }

    private static string GetUniqueDirectoryPath(string path)
    {
        while (Directory.Exists(path) || File.Exists(path))
            path += "_";

        return path;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }
}