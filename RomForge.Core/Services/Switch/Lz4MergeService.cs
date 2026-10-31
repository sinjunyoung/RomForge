using Common;
using LibHac.Ncm;
using NSW.Core;
using NSW.Core.Models;
using RomForge.Core.Models.Switch;
using System.IO;

namespace RomForge.Core.Services.Switch;

public static class Lz4MergeService
{
    private sealed record Routing(List<MergePlan> Plans, List<string> Individuals, List<(string Path, string Reason, bool IsError)> Skipped);

    public static async Task<List<string>> RunAsync(IReadOnlyList<string> inputPaths, string outputDir, int compressLevel, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var routing = BuildRouting(keySet, inputPaths, ct);
        var results = new List<string>();
        int failed = 0;

        foreach (var (path, reason, isError) in routing.Skipped)
        {
            log($"{Path.GetFileName(path)}: {reason}", isError ? LogLevel.Error : LogLevel.Info);

            if (isError)
                failed++;
        }

        int total = routing.Plans.Count + routing.Individuals.Count;

        if (total == 0)
        {
            log("처리할 수 있는 파일이 없습니다.", LogLevel.Error);

            return results;
        }

        Directory.CreateDirectory(outputDir);

        int index = 0;

        foreach (var plan in routing.Plans)
        {
            ct.ThrowIfCancellationRequested();

            index++;

            log($"━━ [{index}/{total}] {DisplayName(plan.Base.Meta)} ({plan.BaseTitleId}) 본편+업데이트 병합 ━━", LogLevel.Highlight);
            log($"구성: 본편 1, 업데이트 1, DLC {plan.Dlcs.Count}", LogLevel.Ok);

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

        foreach (var file in routing.Individuals)
        {
            ct.ThrowIfCancellationRequested();

            index++;

            log($"━━ [{index}/{total}] {Path.GetFileName(file)} ━━", LogLevel.Highlight);

            try
            {
                string path = await NspLz4Service.CompressAsync(file, outputDir, compressLevel, progress, (message, level, _) => log(message, level), ct);

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

    private static Routing BuildRouting(LibHac.Common.Keys.KeySet keySet, IReadOnlyList<string> inputPaths, CancellationToken ct)
    {
        var paths = inputPaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var entries = new List<MergeEntry>();

        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var meta in MetadataReader.GetMetadataFromContainer(keySet, path))
                entries.Add(new MergeEntry(path, meta));
        }

        var plans = new List<MergePlan>();
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = new Dictionary<string, (string Reason, bool IsError)>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in entries.GroupBy(e => LibHacHelper.GetBaseTitleId(e.Meta.TitleId), StringComparer.OrdinalIgnoreCase).Where(g => !string.IsNullOrEmpty(g.Key)))
        {
            var baseEntry = group.Where(e => e.Meta.Type == ContentMetaType.Application).OrderByDescending(e => e.Meta.TitleVersion).FirstOrDefault();
            var updateEntry = group.Where(e => e.Meta.Type == ContentMetaType.Patch).OrderByDescending(e => e.Meta.TitleVersion).FirstOrDefault();

            if (updateEntry == null)
                continue;

            if (baseEntry == null)
            {
                foreach (var file in group.Where(e => e.Meta.Type == ContentMetaType.Patch).Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase))
                    skipped[file] = ("업데이트는 단독으로 압축할 수 없습니다. 본편 NSP를 함께 추가하세요.", true);

                continue;
            }

            var dlcEntries = group
                .Where(e => e.Meta.Type == ContentMetaType.AddOnContent)
                .GroupBy(e => e.Meta.TitleId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(e => e.Meta.TitleVersion).First())
                .ToList();
            var plan = new MergePlan(group.Key, baseEntry, updateEntry, dlcEntries);

            plans.Add(plan);

            foreach (var path in plan.AllPaths)
                merged.Add(path);
        }

        foreach (var entry in entries)
        {
            if (merged.Contains(entry.Path) || skipped.ContainsKey(entry.Path))
                continue;

            if (plans.Any(p => p.BaseTitleId.Equals(LibHacHelper.GetBaseTitleId(entry.Meta.TitleId), StringComparison.OrdinalIgnoreCase)))
                skipped[entry.Path] = ("병합에 사용되지 않는 이전 버전 파일이라 건너뜁니다.", false);
        }

        var individuals = paths.Where(p => !merged.Contains(p) && !skipped.ContainsKey(p)).ToList();

        return new Routing(plans, individuals, [.. skipped.Select(s => (s.Key, s.Value.Reason, s.Value.IsError))]);
    }

    private static string DisplayName(MetadataResult meta)
        => !string.IsNullOrWhiteSpace(meta.KrTitle) ? meta.KrTitle : !string.IsNullOrWhiteSpace(meta.EnTitle) ? meta.EnTitle : meta.TitleId;
}