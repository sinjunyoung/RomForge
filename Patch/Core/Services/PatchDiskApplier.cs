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

        PatchApplyLog.Start(log, name, displayPath);

        try
        {
            using var tempPatch = patch.File.MaterializeAsTempFile(".patch");

            var wrapper = PatchApplyLog.Wrap(progress, name);

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
            PatchApplyLog.Failure(log, patch.Strict, name, displayPath, ex);
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