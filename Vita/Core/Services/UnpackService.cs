using Common;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class UnpackService
{
    public static Task<int> UnpackAsync(List<BatchSourceEntry> entries, string unpackedRoot, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => Task.Run(() => PatchShared.RunWithItemsAsync(entries, log, items => UnpackCoreAsync(items, unpackedRoot, log, progress, ct)), ct);

    private static async Task<int> UnpackCoreAsync(List<SourceItem> items, string unpackedRoot, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var groups = PatchShared.BuildBaseGroups(items, log, ct);
        long totalBytes = groups.Sum(g => g.Owners.Sum(o => PatchShared.GetAllOwnedFiles(o).Sum(f => f.Size)));
        var reporter = new ProgressReporter("언팩 중", string.Empty, totalBytes, progress);
        var writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int written = 0;

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            int groupWritten = await UnpackGroupAsync(group, unpackedRoot, writtenEntries, reporter, log, ct);

            written += groupWritten;

            string name = group.Category == ContentCategory.Addcont ? $"{group.TitleId}/{group.ContentIdSuffix}" : group.TitleId;

            log($"[{group.Category}] {name}: {groupWritten}개 파일 언팩 완료", LogLevel.Ok);
        }

        reporter.ForceReport();

        return written;
    }

    private static async Task<int> UnpackGroupAsync(BaseGroup group, string unpackedRoot, HashSet<string> writtenEntries, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        int count = 0;

        foreach (var owner in group.Owners)
        {
            string prefix = PatchShared.GetRetailBaseFolder(owner.Item.Category);

            foreach (var (relativePath, size) in PatchShared.GetAllOwnedFiles(owner))
            {
                ct.ThrowIfCancellationRequested();

                string entryPath = PatchShared.BuildEntryPath(prefix, group.Category, group.TitleId, group.ContentIdSuffix, relativePath);

                if (!writtenEntries.Add(entryPath))
                    continue;

                byte[] data;

                try
                {
                    data = owner.Item.Accessor!.ReadAllBytes($"{owner.Item.SourcePath}/{relativePath}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log($"[{owner.Item.Category}] {relativePath}: 원본 읽기 실패 - {ex.Message}", LogLevel.Error);
                    reporter.AddProgress(size);
                    continue;
                }

                await WriteFileAsync(unpackedRoot, entryPath, data, size, reporter, ct);

                count++;
            }
        }

        return count;
    }

    private static async Task WriteFileAsync(string unpackedRoot, string entryPath, byte[] data, long estimatedSize, ProgressReporter reporter, CancellationToken ct)
    {
        string root = Path.GetFullPath(unpackedRoot) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(unpackedRoot, entryPath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"허용되지 않는 경로입니다: {entryPath}");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        await PatchShared.WriteStreamWithProgressAsync(stream, data, estimatedSize, reporter, ct);
    }
}