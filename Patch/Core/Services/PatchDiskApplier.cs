using Common;
using Patch.Core.Models;

namespace Patch.Core.Services;

public static class PatchDiskApplier
{
    public static void CopyTo(PatchFileRef file, string destPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        using var src = file.OpenRead();
        using var dst = File.Create(destPath);

        src.CopyTo(dst);
    }

    public static async Task<bool> ApplyAsync(string targetPath, PlannedPatch patch, string displayPath, IProgress<ProgressInfo>? progress, Action<string, LogLevel>? log, CancellationToken ct)
    {
        string name = patch.File.DisplayName;
        string tempOutPath = targetPath + ".patched";

        log?.Invoke($"패치 적용: {name} ➡️ {displayPath}", LogLevel.Info);

        try
        {
            using var tempPatch = patch.File.MaterializeAsTempFile(".patch");

            var wrapper = progress is null ? null : new Progress<ProgressInfo>(p =>
            {
                if (p.Percent > 0)
                    progress.Report(p with { Label = string.IsNullOrEmpty(p.Label) ? $"패치 적용 중 - {name}" : p.Label });
            });

            await UniversalPatcher.ApplyPatchAsync(targetPath, Path.GetFullPath(tempPatch.Path), tempOutPath, wrapper, ct);

            if (!File.Exists(tempOutPath))
            {
                log?.Invoke($"❌ 패치 실패 ({name}): 출력 파일이 생성되지 않았습니다.", LogLevel.Error);
                return false;
            }

            File.Move(tempOutPath, targetPath, overwrite: true);

            return true;
        }
        catch (OperationCanceledException)
        {
            DeleteQuietly(tempOutPath);
            throw;
        }
        catch (Exception ex)
        {
            DeleteQuietly(tempOutPath);

            if (patch.Strict)
                log?.Invoke($"❌ 패치 실패 ({name}): {ex.Message}", LogLevel.Error);
            else
                log?.Invoke($"⚠️ 원본이 맞지 않아 건너뜀: {name} ➡️ {displayPath} ({ex.Message})", LogLevel.Info);

            return false;
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}