using Common;
using LibHac.Ncm;
using NSW.Core;
using NSW.Core.Models;
using RomForge.Core.Models.Switch;
using System.IO;

namespace RomForge.Core.Services.Switch;

public static class Lz4MergeService
{
    public static async Task<List<string>> RunAsync(IReadOnlyList<string> inputPaths, string outputDir, int compressLevel, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var plans = BuildPlans(keySet, inputPaths, ct);
        var results = new List<string>();

        if (plans.Count == 0)
        {
            log("처리할 수 있는 타이틀이 없습니다.", LogLevel.Error);

            return results;
        }

        int index = 0;
        int failed = 0;

        foreach (var plan in plans)
        {
            ct.ThrowIfCancellationRequested();

            index++;

            log($"━━ [{index}/{plans.Count}] {DisplayName(plan.Base.Meta)} ({plan.BaseTitleId}) ━━", LogLevel.Highlight);
            log($"구성: 본편 1, 업데이트 {(plan.Update != null ? 1 : 0)}, DLC {plan.Dlcs.Count}", LogLevel.Ok);

            try
            {
                string path = await MergedTitleWriter.WriteAsync(keySet.Clone(), plan, outputDir, compressLevel, progress, log, ct);

                results.Add(path);

                log($"출력: {path}", LogLevel.Ok);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;

                log($"실패: {ex.Message}", LogLevel.Error);
            }
        }

        log($"완료: 성공 {results.Count}개, 실패 {failed}개", failed == 0 ? LogLevel.Ok : LogLevel.Error);

        return results;
    }

    private static List<MergePlan> BuildPlans(LibHac.Common.Keys.KeySet keySet, IReadOnlyList<string> inputPaths, CancellationToken ct)
    {
        var entries = new List<MergeEntry>();

        foreach (var path in inputPaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            foreach (var meta in MetadataReader.GetMetadataFromContainer(keySet, path))
                entries.Add(new MergeEntry(path, meta));
        }

        var plans = new List<MergePlan>();

        foreach (var group in entries.GroupBy(e => LibHacHelper.GetBaseTitleId(e.Meta.TitleId), StringComparer.OrdinalIgnoreCase).Where(g => !string.IsNullOrEmpty(g.Key)))
        {
            var baseEntry = group.Where(e => e.Meta.Type == ContentMetaType.Application).OrderByDescending(e => e.Meta.TitleVersion).FirstOrDefault();

            if (baseEntry == null)
                continue;

            var updateEntry = group.Where(e => e.Meta.Type == ContentMetaType.Patch).OrderByDescending(e => e.Meta.TitleVersion).FirstOrDefault();
            var dlcEntries = group
                .Where(e => e.Meta.Type == ContentMetaType.AddOnContent)
                .GroupBy(e => e.Meta.TitleId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(e => e.Meta.TitleVersion).First())
                .ToList();

            plans.Add(new MergePlan(group.Key, baseEntry, updateEntry, dlcEntries));
        }

        return plans;
    }

    private static string DisplayName(MetadataResult meta)
        => !string.IsNullOrWhiteSpace(meta.KrTitle) ? meta.KrTitle : !string.IsNullOrWhiteSpace(meta.EnTitle) ? meta.EnTitle : meta.TitleId;
}