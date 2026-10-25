using Common;
using NSW.Core.Models;
using Patch.Core.Services;
using System.IO;

namespace NSW.Core.Services;

public static class NspPatchApplier
{
    public static void ApplyPatch(string patchPath, UnpackResult unpackResult, byte sourceIdOffset, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, string? patchPassword = null)
    {
        string exefsDir = unpackResult.ExefsDirs.GetValueOrDefault(sourceIdOffset, string.Empty);
        string romfsDir = unpackResult.RomfsDirs.GetValueOrDefault(sourceIdOffset, string.Empty);

        if (sourceIdOffset != 0 || unpackResult.ExefsDirs.Count > 1)
            patchPath = ResolveProgramPatchPath(patchPath, GetProgramId(exefsDir, romfsDir));

        int matchedCount = 0;

        if (ArchivePatchSourceFactory.IsArchivePath(patchPath))
        {
            using var archive = ArchivePatchSourceFactory.Open(patchPath, patchPassword);

            matchedCount += ApplyIndex(PatchIndex.FromArchive(archive), exefsDir, romfsDir, progress, log, "한글패치");
            matchedCount += NspExefsPatchApplier.ApplyArchiveExefsPatches(archive, exefsDir, progress, log);
        }
        else if (Directory.Exists(patchPath))
        {
            matchedCount += ApplyIndex(PatchIndex.FromFolder(patchPath), exefsDir, romfsDir, progress, log, "한글패치");
            matchedCount += NspExefsPatchApplier.ApplyFolderExefsPatches(patchPath, exefsDir, progress, log);
        }

        if (matchedCount == 0)
            log("패치 대상 파일이 존재하지 않습니다.", LogLevel.Error);
    }

    private static string GetProgramId(string exefsDir, string romfsDir)
    {
        string dir = exefsDir.Length > 0 ? exefsDir : romfsDir;
        return dir.Length == 0 ? string.Empty : Path.GetFileName(Path.GetDirectoryName(dir)) ?? string.Empty;
    }

    private static string ResolveProgramPatchPath(string patchPath, string programId)
    {
        if (programId.Length == 0)
            return patchPath;

        var (archivePath, scope) = ArchivePatchSourceFactory.SplitScope(patchPath);

        if (scope.Length > 0)
        {
            string trimmed = scope.Trim('/');
            int slash = trimmed.LastIndexOf('/');
            string name = trimmed[(slash + 1)..];

            if (!IsTitleIdName(name) || string.Equals(name, programId, StringComparison.OrdinalIgnoreCase))
                return patchPath;

            return ArchivePatchSourceFactory.CombineScope(archivePath, trimmed[..(slash + 1)] + programId + "/");
        }

        string folder = patchPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folderName = Path.GetFileName(folder);

        if (!IsTitleIdName(folderName) || string.Equals(folderName, programId, StringComparison.OrdinalIgnoreCase))
            return patchPath;

        return Path.Combine(Path.GetDirectoryName(folder) ?? string.Empty, programId);
    }

    private static bool IsTitleIdName(string name) => name.Length == 16 && name.All(Uri.IsHexDigit);

    public static void ApplyDlcPatch(string patchPath, string titleIdStr, string romfsDir, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, string? patchPassword = null)
    {
        int matchedCount = 0;
        string label = $"DLC 패치 ({titleIdStr})";

        if (ArchivePatchSourceFactory.IsArchivePath(patchPath))
        {
            using var archive = ArchivePatchSourceFactory.Open(patchPath, patchPassword);

            matchedCount += ApplyIndex(PatchIndex.FromArchive(archive), string.Empty, romfsDir, progress, log, label);
        }
        else
        {
            if (!Directory.Exists(patchPath))
                return;

            matchedCount += ApplyIndex(PatchIndex.FromFolder(patchPath), string.Empty, romfsDir, progress, log, label);
        }

        if (matchedCount == 0)
            log("패치 대상 파일이 존재하지 않습니다.", LogLevel.Error);
    }

    private static int ApplyIndex(PatchFileIndex index, string exefsDir, string romfsDir, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, string label)
    {
        var dirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (exefsDir.Length > 0 && Directory.Exists(exefsDir))
            dirs[PatchIndex.ExefsPrefix] = exefsDir;

        if (romfsDir.Length > 0 && Directory.Exists(romfsDir))
            dirs[PatchIndex.RomfsPrefix] = romfsDir;

        if (dirs.Count == 0)
            return 0;

        progress.ReportStatus($"{label} 적용 중...", -1);

        var targets = dirs.SelectMany(kv => Directory.EnumerateFiles(kv.Value, "*", SearchOption.AllDirectories).Select(f => $"{kv.Key}/{Path.GetRelativePath(kv.Value, f).Replace('\\', '/')}")).ToList();
        var plan = PatchPlan.Build(index, targets, StringComparer.OrdinalIgnoreCase);

        string? ResolvePath(string key)
        {
            int slash = key.IndexOf('/');

            if (slash < 0 || !dirs.TryGetValue(key[..slash], out string? dir))
                return null;

            return Path.Combine(dir, key[(slash + 1)..].Replace('/', Path.DirectorySeparatorChar));
        }

        int replaced = 0;
        int added = 0;

        foreach (var (key, file) in plan.Overwrites.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            string? dest = ResolvePath(key);

            if (dest == null)
                continue;

            PatchDiskApplier.CopyTo(file, dest);
            log($"{label} 교체: {key}", LogLevel.Info);

            replaced++;
        }

        foreach (var (key, file) in plan.UnmatchedOverwrites.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            string? dest = ResolvePath(key);

            if (dest == null)
                continue;

            PatchDiskApplier.CopyTo(file, dest);
            log($"{label} 추가: {key}", LogLevel.Info);

            added++;
        }

        int attempted = 0;
        int succeeded = 0;

        foreach (string key in plan.PatchedPaths.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            string? dest = ResolvePath(key);

            if (dest == null)
                continue;

            foreach (var patch in plan.GetPatches(key))
            {
                bool ok = PatchDiskApplier.ApplyAsync(dest, patch, key, progress, log, CancellationToken.None).GetAwaiter().GetResult();

                if (ok)
                {
                    attempted++;
                    succeeded++;

                    break;
                }

                if (patch.Strict)
                    attempted++;
            }
        }

        if (replaced > 0)
            log($"{label} 교체 완료: {replaced}개 파일", LogLevel.Ok);

        if (added > 0)
            log($"{label} 추가 완료: {added}개 파일", LogLevel.Ok);

        if (attempted > 0)
            log($"{label} 패치 완료 수: {succeeded}개 / {attempted}개", LogLevel.Ok);

        return replaced + added + attempted;
    }
}