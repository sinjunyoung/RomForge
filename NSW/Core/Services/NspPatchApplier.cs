using Common;
using NSW.Core.Models;
using NSW.HacPack;
using Patch.Core.Interfaces;
using Patch.Core.Services;
using System.IO;

namespace NSW.Core.Services;

public static class NspPatchApplier
{
    public static void ApplyPatch(string patchPath, UnpackResult unpackResult, byte sourceIdOffset, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, string? patchPassword = null)
    {
        string exefsDir = unpackResult.ExefsDirs.GetValueOrDefault(sourceIdOffset, string.Empty);
        string romfsDir = unpackResult.RomfsDirs.GetValueOrDefault(sourceIdOffset, string.Empty);

        unpackResult.VirtualParts.TryGetValue(sourceIdOffset, out var parts);

        bool diskExefs = exefsDir.Length > 0 && Directory.Exists(exefsDir);
        IPatchTarget? exefsTarget = parts?.Exefs != null ? parts.Exefs : diskExefs ? new DiskPatchTarget(exefsDir) : null;
        IFileStore? exefsStore = parts?.Exefs != null ? parts.Exefs : diskExefs ? new DiskFileStore(exefsDir) : null;
        IPatchTarget? romfsTarget = unpackResult.VirtualRomfsPrograms.TryGetValue(sourceIdOffset, out var virtualRomfs) ? virtualRomfs : DiskTarget(romfsDir);
        int exefsCount = unpackResult.ExefsDirs.Count + unpackResult.VirtualParts.Values.Count(p => p.Exefs != null);

        if (sourceIdOffset != 0 || exefsCount > 1)
            patchPath = ResolveProgramPatchPath(patchPath, parts?.ProgramId ?? GetProgramId(exefsDir, romfsDir));

        int matchedCount = 0;

        if (ArchivePatchSourceFactory.IsArchivePath(patchPath))
        {
            bool keepAlive = exefsTarget is VirtualRomfs || romfsTarget is VirtualRomfs;
            var archive = OpenArchive(patchPath, patchPassword, keepAlive, unpackResult.Resources);

            try
            {
                matchedCount += ApplyIndex(PatchIndex.FromArchive(archive), exefsTarget, romfsTarget, progress, log, "한글패치");
                matchedCount += NspExefsPatchApplier.ApplyArchiveExefsPatches(archive, exefsStore, progress, log);
            }
            finally
            {
                if (!keepAlive)
                    archive.Dispose();
            }
        }
        else if (Directory.Exists(patchPath))
        {
            matchedCount += ApplyIndex(PatchIndex.FromFolder(patchPath), exefsTarget, romfsTarget, progress, log, "한글패치");
            matchedCount += NspExefsPatchApplier.ApplyFolderExefsPatches(patchPath, exefsStore, progress, log);
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
        var target = DiskTarget(romfsDir);

        if (target != null)
            ApplyDlcPatch(patchPath, titleIdStr, target, [], progress, log, patchPassword);
    }

    public static void ApplyDlcPatch(string patchPath, string titleIdStr, IPatchTarget romfsTarget, ICollection<IDisposable> owner, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, string? patchPassword = null)
    {
        int matchedCount = 0;
        string label = $"DLC 패치 ({titleIdStr})";

        if (ArchivePatchSourceFactory.IsArchivePath(patchPath))
        {
            bool keepAlive = romfsTarget is VirtualRomfs;
            var archive = OpenArchive(patchPath, patchPassword, keepAlive, owner);

            try
            {
                matchedCount += ApplyIndex(PatchIndex.FromArchive(archive), null, romfsTarget, progress, log, label);
            }
            finally
            {
                if (!keepAlive)
                    archive.Dispose();
            }
        }
        else
        {
            if (!Directory.Exists(patchPath))
                return;

            matchedCount += ApplyIndex(PatchIndex.FromFolder(patchPath), null, romfsTarget, progress, log, label);
        }

        if (matchedCount == 0)
            log("패치 대상 파일이 존재하지 않습니다.", LogLevel.Error);
    }

    private static IArchivePatchSource OpenArchive(string patchPath, string? password, bool keepAlive, ICollection<IDisposable> owner)
    {
        var archive = ArchivePatchSourceFactory.Open(patchPath, password);

        if (keepAlive)
            owner.Add(archive);

        return archive;
    }

    private static DiskPatchTarget? DiskTarget(string directory) => directory.Length > 0 && Directory.Exists(directory) ? new DiskPatchTarget(directory) : null;

    private static int ApplyIndex(PatchFileIndex index, IPatchTarget? exefsTarget, IPatchTarget? romfsTarget, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, string label)
    {
        var targetsByPrefix = new Dictionary<string, IPatchTarget>(StringComparer.OrdinalIgnoreCase);

        if (exefsTarget != null)
            targetsByPrefix[PatchIndex.ExefsPrefix] = exefsTarget;

        if (romfsTarget != null)
            targetsByPrefix[PatchIndex.RomfsPrefix] = romfsTarget;

        if (targetsByPrefix.Count == 0)
            return 0;

        progress.ReportStatus($"{label} 적용 중...", -1);

        var targets = targetsByPrefix.SelectMany(kv => kv.Value.EnumerateRelativeFiles().Select(f => $"{kv.Key}/{f}")).ToList();
        var plan = PatchPlan.Build(index, targets, StringComparer.OrdinalIgnoreCase);

        (IPatchTarget Target, string Relative)? Resolve(string key)
        {
            int slash = key.IndexOf('/');

            if (slash < 0 || !targetsByPrefix.TryGetValue(key[..slash], out var target))
                return null;

            return (target, key[(slash + 1)..]);
        }

        int replaced = 0;
        int added = 0;

        foreach (var (key, file) in plan.Overwrites.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            var resolved = Resolve(key);

            if (resolved == null)
                continue;

            resolved.Value.Target.Overwrite(resolved.Value.Relative, file);
            log($"{label} 교체: {key}", LogLevel.Info);

            replaced++;
        }

        foreach (var (key, file) in plan.UnmatchedOverwrites.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            var resolved = Resolve(key);

            if (resolved == null)
                continue;

            resolved.Value.Target.Overwrite(resolved.Value.Relative, file);
            log($"{label} 추가: {key}", LogLevel.Info);

            added++;
        }

        int attempted = 0;
        int succeeded = 0;

        foreach (string key in plan.PatchedPaths.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var resolved = Resolve(key);

            if (resolved == null)
                continue;

            foreach (var patch in plan.GetPatches(key))
            {
                bool ok = resolved.Value.Target.PatchAsync(resolved.Value.Relative, patch, key, progress, log, CancellationToken.None).GetAwaiter().GetResult();

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