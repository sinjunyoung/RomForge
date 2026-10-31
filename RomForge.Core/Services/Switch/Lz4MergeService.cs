using Common;
using LibHac.Ncm;
using NSW.Core;
using NSW.Core.Models;
using NSW.Core.Services;
using System.IO;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

public static class Lz4MergeService
{
    private sealed record Entry(string Path, MetadataResult Meta);

    public static async Task<List<string>> RunAsync(IReadOnlyList<string> inputPaths, string outputDir, int compressLevel, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var entries = new List<Entry>();

        foreach (var path in inputPaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            foreach (var meta in MetadataReader.GetMetadataFromContainer(keySet, path))
                entries.Add(new Entry(path, meta));
        }

        var groups = entries
            .Select(e => (BaseTitleId: LibHacHelper.GetBaseTitleId(e.Meta.TitleId), Entry: e))
            .Where(x => !string.IsNullOrEmpty(x.BaseTitleId))
            .GroupBy(x => x.BaseTitleId, x => x.Entry, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<string>();

        if (groups.Count == 0)
        {
            log("처리할 수 있는 타이틀이 없습니다.", LogLevel.Error);

            return results;
        }

        Directory.CreateDirectory(outputDir);

        int index = 0;
        int failed = 0;

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            index++;

            var baseEntry = group.Where(e => e.Meta.Type == ContentMetaType.Application).OrderByDescending(e => e.Meta.TitleVersion).FirstOrDefault();
            string title = baseEntry != null ? DisplayName(baseEntry.Meta) : group.Key;

            log($"━━ [{index}/{groups.Count}] {title} ({group.Key}) ━━", LogLevel.Highlight);

            if (baseEntry == null)
            {
                log("본편(BASE)이 없어 건너뜁니다.", LogLevel.Error);
                failed++;

                continue;
            }

            var updateEntry = group.Where(e => e.Meta.Type == ContentMetaType.Patch).OrderByDescending(e => e.Meta.TitleVersion).FirstOrDefault();
            var dlcEntries = group
                .Where(e => e.Meta.Type == ContentMetaType.AddOnContent)
                .GroupBy(e => e.Meta.TitleId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(e => e.Meta.TitleVersion).First())
                .ToList();
            var dlcTitleIds = dlcEntries
                .GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Meta.TitleId).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            log($"구성: 본편 1, 업데이트 {(updateEntry != null ? 1 : 0)}, DLC {dlcEntries.Count}", LogLevel.Ok);

            string workDir = Path.Combine(outputDir, ".lz4merge_" + Guid.NewGuid().ToString("N")[..8]);
            var req = new BuildRequest(baseEntry.Path, updateEntry?.Path ?? string.Empty, [.. dlcEntries.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase)], string.Empty, workDir)
            {
                StreamRomfs = true,
                DlcTitleIdsByFile = dlcTitleIds,
                OverrideKeyGeneration = 1
            };

            try
            {
                string built = await NspBuildService.Run(req, BuildMode.FullProcess, compressLevel, progress, log, ct);
                string finalPath = Common.Utils.GetUniqueFilePath(Path.Combine(outputDir, Path.GetFileName(built)));

                File.Move(built, finalPath);
                results.Add(finalPath);

                log($"출력: {finalPath}", LogLevel.Ok);
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
            finally
            {
                TryDeleteDirectory(workDir);
            }
        }

        log($"완료: 성공 {results.Count}개, 실패 {failed}개", failed == 0 ? LogLevel.Ok : LogLevel.Error);

        return results;
    }

    private static string DisplayName(MetadataResult meta)
        => !string.IsNullOrWhiteSpace(meta.KrTitle) ? meta.KrTitle : !string.IsNullOrWhiteSpace(meta.EnTitle) ? meta.EnTitle : meta.TitleId;

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"작업 폴더 정리 실패: {ex.Message}");
        }
    }
}