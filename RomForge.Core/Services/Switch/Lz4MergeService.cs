using Common.Models;
using LibHac.Ncm;
using NSW.Core;
using NSW.Core.Models;
using NSW.Core.Services;
using RomForge.Core.Models.Switch;
using System.IO;

namespace RomForge.Core.Services.Switch;

public static class Lz4MergeService
{
    private sealed record Routing(List<MergePlan> Plans, List<string> Individuals, List<(string Path, string Reason, LogLevel Level)> Skipped);

    private sealed record Job(MergePlan? Plan, string? File, int Order);

    public static async Task<List<string>> RunAsync(IReadOnlyList<string> inputPaths, string outputDir, int compressLevel, Action<int> jobsPlanned, Func<IReadOnlyList<string>, bool> isCompleted,
        Func<IReadOnlyList<string>, bool, IProgress<ProgressInfo>> progressFactory, Action<IReadOnlyList<string>, Lz4JobResult> jobFinished, Action<string, LogLevel> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var routing = BuildRouting(keySet, inputPaths, ct);
        var results = new List<string>();
        int failed = 0;

        foreach (var (path, reason, level) in routing.Skipped)
        {
            log($"{Path.GetFileName(path)}: {reason}", level);

            if (level == LogLevel.Error)
                failed++;
        }

        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < inputPaths.Count; i++)
        {
            if (!string.IsNullOrEmpty(inputPaths[i]))
                order.TryAdd(inputPaths[i], i);
        }

        var jobs = routing.Plans
            .Select(p => new Job(p, null, p.AllPaths.Min(path => order[path])))
            .Concat(routing.Individuals.Select(f => new Job(null, f, order[f])))
            .OrderBy(j => j.Order)
            .ToList();

        int total = jobs.Count;

        if (total == 0)
        {
            log("처리할 수 있는 파일이 없습니다.", LogLevel.Error);
            return results;
        }

        Directory.CreateDirectory(outputDir);
        jobsPlanned(total);

        void quietLog(string message, LogLevel level)
        {
            if (level is LogLevel.Warning or LogLevel.Error)
                log(message, level);
        }

        int index = 0;

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();

            index++;

            bool isBuild = job.Plan != null;
            IReadOnlyList<string> jobPaths = isBuild ? job.Plan!.AllPaths : new[] { job.File! };

            if (isCompleted(jobPaths))
            {
                string name = job.Plan != null ? DisplayName(job.Plan.Base.Meta) : Path.GetFileName(job.File!);

                jobFinished(jobPaths, Lz4JobResult.AlreadyDone);
                log($"[{index}/{total}] {name}: 이미 완료되어 건너뜁니다.", LogLevel.Info);
                continue;
            }

            if (job.Plan != null)
            {
                log($"━━ [{index}/{total}] {DisplayName(job.Plan.Base.Meta)} ({job.Plan.BaseTitleId}) 본편 Lz4 압축 ━━", LogLevel.Highlight);
                log($"구성: 본편 1, 업데이트 {(job.Plan.Update != null ? 1 : 0)}, DLC {job.Plan.Dlcs.Count}", LogLevel.Ok);
            }
            else
                log($"━━ [{index}/{total}] {Path.GetFileName(job.File!)} ━━", LogLevel.Highlight);

            try
            {
                string? path = job.Plan != null
                    ? await NspBuildService.Run(CreateRequest(job.Plan, outputDir), BuildMode.FullProcess, compressLevel, progressFactory(jobPaths, true), quietLog, ct)
                    : await NspLz4Service.CompressAsync(job.File!, outputDir, compressLevel, progressFactory(jobPaths, false), (message, level, _) => quietLog(message, level), ct);

                if (path == null)
                {
                    jobFinished(jobPaths, Lz4JobResult.Skipped);
                    continue;
                }

                results.Add(path);
                jobFinished(jobPaths, Lz4JobResult.Done);
                log($"완료: {Path.GetFileName(path)}", LogLevel.Ok);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;

                jobFinished(jobPaths, Lz4JobResult.Failed);
                log($"실패: {ex.Message}", LogLevel.Error);
            }
        }

        if (failed > 0)
            log($"실패 {failed}개", LogLevel.Error);

        return results;
    }

    private static NSW.Core.Models.BuildRequest CreateRequest(MergePlan plan, string outputDir)
    {
        string basePath = plan.Base.Path;
        string updatePath = plan.Update != null && !plan.Update.Path.Equals(basePath, StringComparison.OrdinalIgnoreCase) ? plan.Update.Path : string.Empty;
        var dlcPaths = plan.Dlcs.Select(d => d.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        bool hasIntegratedDlc = dlcPaths.Any(p => p.Equals(basePath, StringComparison.OrdinalIgnoreCase) || (updatePath.Length > 0 && p.Equals(updatePath, StringComparison.OrdinalIgnoreCase)));
        Dictionary<string, HashSet<string>>? dlcTitleIds = hasIntegratedDlc
            ? null
            : plan.Dlcs
                .GroupBy(d => d.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(d => d.Meta.TitleId).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var dlcCompress = plan.Dlcs.ToDictionary(d => d.Meta.TitleId, _ => true, StringComparer.OrdinalIgnoreCase);

        return new NSW.Core.Models.BuildRequest(basePath, updatePath, dlcPaths, string.Empty, outputDir)
        {
            DlcTitleIdsByFile = dlcTitleIds,
            CompressRomfs = true,
            DlcCompressRomfs = dlcCompress,
            StreamRomfs = true,
            OutputFileName = Path.GetFileNameWithoutExtension(basePath) + "_Lz4.nsp"
        };
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
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            var plan = new MergePlan(group.Key, baseEntry, updateEntry, dlcEntries);

            plans.Add(plan);

            foreach (var path in plan.AllPaths)
                used.Add(path);
        }

        var individuals = new List<string>();
        var skipped = new List<(string Path, string Reason, LogLevel Level)>();

        foreach (var path in paths.Where(p => !used.Contains(p)))
        {
            var metas = entries.Where(e => e.Path.Equals(path, StringComparison.OrdinalIgnoreCase)).Select(e => e.Meta).ToList();

            if (metas.Any(m => plans.Any(p => p.BaseTitleId.Equals(LibHacHelper.GetBaseTitleId(m.TitleId), StringComparison.OrdinalIgnoreCase))))
            {
                skipped.Add((path, "압축에 사용되지 않는 이전 버전 파일이라 건너뜁니다.", LogLevel.Info));
                continue;
            }

            if (metas.Any(m => m.Type == ContentMetaType.Patch))
            {
                skipped.Add((path, "업데이트는 단독으로 Lz4 압축할 수 없어 건너뜁니다. 본편 NSP를 함께 추가하면 본편과 함께 압축됩니다.", LogLevel.Warning));
                continue;
            }

            individuals.Add(path);
        }

        return new Routing(plans, individuals, skipped);
    }

    private static string DisplayName(MetadataResult meta) => !string.IsNullOrWhiteSpace(meta.KrTitle) ? meta.KrTitle : !string.IsNullOrWhiteSpace(meta.EnTitle) ? meta.EnTitle : meta.TitleId;
}