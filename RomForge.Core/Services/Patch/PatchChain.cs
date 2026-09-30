using Common;
using Patch.Core;
using System.IO;

namespace RomForge.Core.Services.Patch;

public static class PatchChain
{
    public static void Validate(IReadOnlyList<string> patchPaths)
    {
        if (patchPaths.Count > 1 && patchPaths.Any(p => Path.GetExtension(p).Equals(".dcp", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("DCP 패치는 다중 패치로 연속 적용할 수 없습니다.");
    }

    public static async Task ApplyAsync(string sourcePath, IReadOnlyList<string> patchPaths, string outputPath, string workDir, Action<string, LogLevel> log, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        if (patchPaths.Count == 1)
        {
            await UniversalPatcher.ApplyPatchAsync(sourcePath, patchPaths[0], outputPath, progress, ct);

            return;
        }

        int total = patchPaths.Count;
        var intermediates = new List<string>();
        string current = sourcePath;

        try
        {
            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();

                bool isLast = i == total - 1;
                string target = outputPath;

                if (!isLast)
                {
                    target = Utils.GetUniqueFilePath(Path.Combine(workDir, $"_chain{i + 1}_" + Path.GetFileName(outputPath)));
                    intermediates.Add(target);
                }

                int step = i;
                var stepProgress = new Progress<ProgressInfo>(info => progress.Report(info with
                {
                    Percent = (step * 100 + info.Percent) / total,
                    Label = $"[{step + 1}/{total}] {info.Label}"
                }));

                log($"다중 패치 {i + 1}/{total} 적용 중: {Path.GetFileName(patchPaths[i])}", LogLevel.Highlight);

                await UniversalPatcher.ApplyPatchAsync(current, patchPaths[i], target, stepProgress, ct);

                if (i > 0)
                    TryDelete(current);

                current = target;
            }
        }
        finally
        {
            foreach (var path in intermediates)
                TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}