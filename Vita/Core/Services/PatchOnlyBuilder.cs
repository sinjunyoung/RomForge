using Common;
using System.IO.Compression;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class PatchOnlyBuilder
{
    public static Task<PatchOnlyResult> BuildFromEntriesAsync(List<BatchSourceEntry> entries, string defaultPatchPath, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => PatchShared.RunWithItemsAsync(entries, log, items => BuildCoreAsync(items, defaultPatchPath, outputZipPath, target, log, progress, ct));

    private static async Task<PatchOnlyResult> BuildCoreAsync(List<SourceItem> items, string defaultPatchPath, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var patchContexts = new Dictionary<string, PatchContext>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var groups = PatchShared.BuildGroups(items, defaultPatchPath, patchContexts, target, log, ct);
            return await WriteAsync(groups, outputZipPath, target, log, progress, ct);
        }
        finally
        {
            foreach (var patchCtx in patchContexts.Values)
                patchCtx.Accessor.Dispose();
        }
    }

    internal static async Task<PatchOnlyResult> WriteAsync(List<MergeGroup> groups, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        long totalBytes = groups.Sum(g => g.Targets.Sum(t => t.EstimatedSize));
        var reporter = new ProgressReporter("패치 적용 중", string.Empty, totalBytes, progress);
        var writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int matched = 0;
        int success = 0;

        Directory.CreateDirectory(Path.GetDirectoryName(outputZipPath)!);

        using var zipStream = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Create);

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            int groupMatched = 0;
            int groupSuccess = 0;

            foreach (var t in group.Targets)
            {
                ct.ThrowIfCancellationRequested();

                bool strict = t.IsStrict;

                if (strict)
                    groupMatched++;

                string prefix = PatchShared.GetPatchedPrefix(group.Category, target);
                string entryPath = PatchShared.BuildEntryPath(prefix, group, t.RelativePath);

                if (!writtenEntries.Add(entryPath))
                {
                    log($"[{group.Category}] {t.RelativePath}: 이미 같은 경로로 추가된 항목이라 건너뜀 (중복)", LogLevel.Highlight);
                    continue;
                }

                try
                {
                    byte[]? outputBytes = await PatchShared.ResolveTargetBytesAsync(t, log, ct);

                    if (outputBytes is null)
                    {
                        writtenEntries.Remove(entryPath);
                        reporter.AddProgress(t.EstimatedSize);
                        continue;
                    }

                    if (!strict)
                        groupMatched++;

                    var zipEntry = zip.CreateEntry(entryPath, CompressionLevel.NoCompression);

                    await PatchShared.WriteEntryWithProgressAsync(zipEntry, outputBytes, t.EstimatedSize, reporter, ct);

                    groupSuccess++;
                }
                catch (Exception ex)
                {
                    if (!strict)
                        groupMatched++;

                    log($"[{group.Category}] {t.RelativePath}: 처리 실패 - {ex.Message}", LogLevel.Error);
                    reporter.AddProgress(t.EstimatedSize);
                }
            }

            matched += groupMatched;
            success += groupSuccess;

            LogGroupResult(log, group, groupMatched, groupSuccess);

            if (target == OutputTarget.Emu)
            {
                foreach (var owner in group.Owners)
                    PatchShared.WriteLicenseEntry(zip, writtenEntries, owner);
            }
        }

        reporter.ForceReport();

        return new PatchOnlyResult { MatchedCandidates = matched, PatchedSuccessfully = success };
    }

    private static void LogGroupResult(Action<string, LogLevel> log, MergeGroup group, int matched, int success)
    {
        if (matched == 0)
            return;

        string name = group.Category == ContentCategory.Addcont ? $"{group.TitleId}/{group.ContentIdSuffix}" : group.TitleId;

        if (success == matched)
            log($"[{group.Category}] {name}: {matched}개 중 {success}개 패치 완료", LogLevel.Ok);
        else
            log($"[{group.Category}] {name}: {matched}개 중 {success}개만 패치 성공 ({matched - success}개 실패)", LogLevel.Error);
    }
}