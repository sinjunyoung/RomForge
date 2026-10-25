using Common;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class RebuildService
{
    public static async Task<MergeResult> RebuildAsync(List<UnpackedTitle> titles, string outputZipPath, OutputTarget target, bool mergeIntoGame, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var entries = titles.Select(t => t.FolderPath).Distinct(StringComparer.OrdinalIgnoreCase).Select(path => new BatchSourceEntry { Kind = SourceKind.ZipOrFolder, Path = path }).ToList();
        string patchPath = titles.Select(t => t.PatchPath).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? string.Empty;

        if (mergeIntoGame)
            return await Task.Run(() => PatchOutputBuilder.BuildMergedFromEntriesAsync(entries, patchPath, outputZipPath, target, log, progress, ct), ct);

        var result = await Task.Run(() => PatchOnlyBuilder.BuildFromEntriesAsync(entries, patchPath, outputZipPath, target, log, progress, ct), ct);

        return new MergeResult { TotalFiles = result.PatchedSuccessfully, PatchCandidates = result.MatchedCandidates, PatchedSuccessfully = result.PatchedSuccessfully };
    }
}