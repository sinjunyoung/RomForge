using Common;
using Patch.Core.Formats;
using Patch.Core.Services;
using Path = System.IO.Path;

namespace NSW.M1.Core.Services;

public static class NspXdeltaPatcher
{
    private readonly record struct XdeltaCandidate(string TargetFileName, string? AbsoluteExactPath, PatchFileRef PatchRef, string DisplayName);

    private static ILookup<string, string> BuildFileNameIndex(string? dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return Array.Empty<string>().ToLookup(f => f, StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                         .ToLookup(Path.GetFileName, StringComparer.OrdinalIgnoreCase)!;
    }

    private static IEnumerable<string> FindTargetsByContains(ILookup<string, string> index, string targetFileName)
    {
        foreach (var group in index)
        {
            if (targetFileName.Contains(group.Key, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var f in group)
                    yield return f;
            }
        }
    }

    private static int ApplyXdeltaCandidates(List<XdeltaCandidate> candidates, string? exefsDir, string romfsDir, string displayRoot, bool isDlc, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log)
    {
        int count = 0;
        int successCount = 0;
        string label = isDlc ? "DLC xdelta" : "xdelta";
        var exefsIndex = BuildFileNameIndex(exefsDir);
        var romfsIndex = BuildFileNameIndex(romfsDir);

        foreach (var candidate in candidates)
        {
            var targetFiles = new List<string>();

            if (candidate.AbsoluteExactPath != null && File.Exists(candidate.AbsoluteExactPath))
                targetFiles.Add(candidate.AbsoluteExactPath);
            else
            {
                targetFiles.AddRange(FindTargetsByContains(exefsIndex, candidate.TargetFileName));
                targetFiles.AddRange(FindTargetsByContains(romfsIndex, candidate.TargetFileName));
            }

            if (targetFiles.Count == 0)
            {
                log($"⚠️ {label} 대상 원본 파일을 찾을 수 없음: {candidate.TargetFileName}", LogLevel.Info);
                continue;
            }

            using var tempPatch = candidate.PatchRef.MaterializeAsTempFile(".xdelta");

            foreach (var targetPath in targetFiles.Distinct())
            {
                count++;

                if (ApplyXdeltaToTarget(tempPatch.Path, targetPath, displayRoot, progress, log, isDlc: isDlc, displayName: candidate.DisplayName))
                    successCount++;
            }
        }

        if (count > 0)
            log($"{label} 패치 완료 수: {successCount}개 / {count}개", LogLevel.Ok);

        return count;
    }

    public static int ApplyFolderXdelta(string patchPath, string exefsDir, string romfsDir, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log)
    {
        var xdeltaFiles = Directory.EnumerateFiles(patchPath, "*.xdelta", SearchOption.AllDirectories)
                                   .OrderBy(f => f)
                                   .ToList();

        if (xdeltaFiles.Count == 0)
            return 0;

        progress.Report((-1, "xdelta 바이너리 패치 적용 중..."));
        log($"발견된 xdelta 패치 수: {xdeltaFiles.Count}개", LogLevel.Info);

        string unpackedRoot = Path.GetDirectoryName(exefsDir)!;
        var candidates = new List<XdeltaCandidate>();

        foreach (var xdeltaPath in xdeltaFiles)
        {
            string targetFileName = Path.GetFileNameWithoutExtension(xdeltaPath);
            string relativePath = Path.GetRelativePath(patchPath, xdeltaPath);
            string relativeTargetKey = Path.Combine(Path.GetDirectoryName(relativePath) ?? string.Empty, targetFileName);
            string absoluteExactPath = Path.Combine(unpackedRoot, relativeTargetKey);

            candidates.Add(new XdeltaCandidate(targetFileName, absoluteExactPath, PatchFileRef.FromDisk(xdeltaPath), Path.GetFileName(xdeltaPath)));
        }

        return ApplyXdeltaCandidates(candidates, exefsDir, romfsDir, unpackedRoot, false, progress, log);
    }

    public static int ApplyFolderXdeltaDlc(string patchPath, string romfsDir, string titleIdStr, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log)
    {
        var xdeltaFiles = Directory.EnumerateFiles(patchPath, "*.xdelta", SearchOption.AllDirectories)
                                   .OrderBy(f => f)
                                   .ToList();

        if (xdeltaFiles.Count == 0)
            return 0;

        progress.Report((-1, $"DLC xdelta 패치 적용 중... ({titleIdStr})"));
        log($"발견된 DLC xdelta 패치 수: {xdeltaFiles.Count}개", LogLevel.Info);

        var candidates = xdeltaFiles
            .Select(xdeltaPath => new XdeltaCandidate(
                Path.GetFileNameWithoutExtension(xdeltaPath),
                null,
                PatchFileRef.FromDisk(xdeltaPath),
                Path.GetFileName(xdeltaPath)))
            .ToList();

        return ApplyXdeltaCandidates(candidates, null, romfsDir, romfsDir, true, progress, log);
    }

    private static (string EntryName, string TargetFileName) ParseArchiveXdeltaKey(string key)
    {
        string entryName = key.Contains('/') ? key[(key.LastIndexOf('/') + 1)..] : key;
        string targetFileName = Path.GetFileNameWithoutExtension(entryName);

        return (entryName, targetFileName);
    }

    public static int ApplyArchiveXdelta(IArchivePatchSource archive, string exefsDir, string romfsDir, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log)
    {
        var xdeltaKeys = archive.EntryPaths
            .Where(k => k.EndsWith(".xdelta", StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k)
            .ToList();

        if (xdeltaKeys.Count == 0)
            return 0;

        progress.Report((-1, "xdelta 바이너리 패치 적용 중..."));
        log($"발견된 xdelta 패치 수: {xdeltaKeys.Count}개", LogLevel.Info);

        string unpackedRoot = Path.GetDirectoryName(exefsDir) is { Length: > 0 } d ? d : Path.GetDirectoryName(romfsDir) ?? string.Empty;
        var candidates = new List<XdeltaCandidate>();

        foreach (string key in xdeltaKeys)
        {
            var (entryName, targetFileName) = ParseArchiveXdeltaKey(key);
            int lastSlash = key.LastIndexOf('/');
            string relDir = lastSlash < 0 ? string.Empty : key[..lastSlash];
            string relativeTargetKey = relDir.Length == 0 ? targetFileName : $"{relDir}/{targetFileName}";
            string absoluteExactPath = Path.Combine(unpackedRoot, relativeTargetKey.Replace('/', Path.DirectorySeparatorChar));
            var entry = archive.FindEntry(key);

            if (entry == null)
                continue;

            candidates.Add(new XdeltaCandidate(targetFileName, absoluteExactPath, PatchFileRef.FromArchiveEntry(entry), entryName));
        }

        return ApplyXdeltaCandidates(candidates, exefsDir, romfsDir, unpackedRoot, false, progress, log);
    }

    public static int ApplyArchiveXdeltaDlc(IArchivePatchSource archive, string romfsDir, string titleIdStr, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log)
    {
        var xdeltaKeys = archive.EntryPaths
            .Where(k => k.EndsWith(".xdelta", StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k)
            .ToList();

        if (xdeltaKeys.Count == 0)
            return 0;

        progress.Report((-1, $"DLC xdelta 패치 적용 중... ({titleIdStr})"));
        log($"발견된 DLC xdelta 패치 수: {xdeltaKeys.Count}개", LogLevel.Info);

        var candidates = new List<XdeltaCandidate>();

        foreach (string key in xdeltaKeys)
        {
            var (entryName, targetFileName) = ParseArchiveXdeltaKey(key);
            var entry = archive.FindEntry(key);

            if (entry == null)
                continue;

            candidates.Add(new XdeltaCandidate(targetFileName, null, PatchFileRef.FromArchiveEntry(entry), entryName));
        }

        return ApplyXdeltaCandidates(candidates, null, romfsDir, romfsDir, true, progress, log);
    }

    private static bool ApplyXdeltaToTarget(string xdeltaPath, string targetPath, string displayRoot, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, bool isDlc = false, string? displayName = null)
    {
        string displayPath = Path.GetRelativePath(displayRoot, targetPath);
        string prefix = isDlc ? "DLC xdelta" : "xdelta";
        string shownName = displayName ?? Path.GetFileName(xdeltaPath);

        log($"{prefix} 패치 적용: {shownName} ➡️ {displayPath}", LogLevel.Info);

        string tempOutPath = targetPath + ".patched";

        try
        {
            var wrapper = new Progress<ProgressInfo>(p =>
            {
                int currentStep = 80 + (int)(p.Percent * 0.1);

                if (currentStep > 80)
                    progress?.Report((currentStep, string.Empty));
            });

            Xdelta3.ApplyPatch(targetPath, Path.GetFullPath(xdeltaPath), tempOutPath, wrapper);

            if (!File.Exists(tempOutPath))
            {
                log($"❌ {prefix} 패치 실패 ({shownName}): 출력 파일이 생성되지 않았습니다.", LogLevel.Error);
                return false;
            }

            File.Move(tempOutPath, targetPath, overwrite: true);

            return true;
        }
        catch (Exception ex)
        {
            log($"❌ {prefix} 패치 실패 ({shownName}): {ex.Message}", LogLevel.Error);

            if (File.Exists(tempOutPath))
                File.Delete(tempOutPath);

            return false;
        }
    }
}