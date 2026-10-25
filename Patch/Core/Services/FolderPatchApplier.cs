using Common;
using Patch.Core.Enums;
using Patch.Core.Models;

namespace Patch.Core.Services;

public static class FolderPatchApplier
{
    public static async Task<bool> ApplyAsync(string? patchRoot, string folderName, string targetDir, Func<PatchFileEntry, string, CancellationToken, Task> onOverwrite, Func<PatchFileEntry, string, CancellationToken, Task> onBinaryPatch, Action<string, LogLevel>? log, CancellationToken ct = default)
    {
        string? patchDir = PatchFolderResolver.FindSubDir(patchRoot, folderName);

        if (patchDir == null)
            return false;

        var index = PatchFileIndex.Build(patchDir);
        bool matched = false;

        foreach (var entry in index.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.Kind == PatchFileKind.Overwrite)
            {
                await onOverwrite(entry, targetDir, ct);

                matched = true;

                log?.Invoke($"덮어쓰기: {entry.BaseName}", LogLevel.Info);

                continue;
            }

            string? targetFile = ResolveTargetFile(targetDir, entry);

            if (targetFile == null)
            {
                log?.Invoke($"⚠️ 패치 대상 파일을 찾을 수 없음: {entry.BaseName}", LogLevel.Info);
                continue;
            }

            await onBinaryPatch(entry, targetFile, ct);

            matched = true;

            log?.Invoke($"패치 완료: {entry.File.DisplayName}", LogLevel.Info);
        }

        if (!matched)
            log?.Invoke("패치 대상 파일이 존재하지 않습니다.", LogLevel.Error);

        return matched;
    }

    private static string? ResolveTargetFile(string targetDir, PatchFileEntry entry)
    {
        string exactDir = Path.Combine(targetDir, entry.RelativeDir);

        if (Directory.Exists(exactDir))
        {
            string? hit = Directory.EnumerateFiles(exactDir, entry.BaseName + ".*").FirstOrDefault();

            if (hit != null)
                return hit;
        }

        return Directory.Exists(targetDir) ? Directory.EnumerateFiles(targetDir, entry.BaseName + ".*", SearchOption.AllDirectories).FirstOrDefault() : null;
    }
}