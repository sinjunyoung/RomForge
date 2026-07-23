using Common;
using NSW.M1.Core.Models;
using Patch.Core.Formats;
using Path = System.IO.Path;

namespace NSW.M1.Core.Services;

public static class NspPatchApplier
{
    public static void ApplyPatch(string patchDir, UnpackResult unpackResult, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log)
    {
        string exefsDir = unpackResult.ExefsDirs.GetValueOrDefault((byte)0, string.Empty);
        string romfsDir = unpackResult.RomfsDirs.GetValueOrDefault((byte)0, string.Empty);
        string patchExefs = Path.Combine(patchDir, "exefs");
        string patchRomfs = Path.Combine(patchDir, "romfs");

        if (Directory.Exists(patchExefs))
        {
            progress.Report((-1, "한글패치 ExeFS 병합 중..."));
            log($"  한글패치 ExeFS 병합: {patchExefs}", LogLevel.Info);
            MergeDirectory(patchExefs, exefsDir);
        }
        if (Directory.Exists(patchRomfs))
        {
            progress.Report((-1, "한글패치 RomFS 병합 중..."));
            log($"  한글패치 RomFS 병합: {patchRomfs}", LogLevel.Info);
            MergeDirectory(patchRomfs, romfsDir);
        }

        if (Directory.Exists(patchDir))
        {
            var xdeltaFiles = Directory.EnumerateFiles(patchDir, "*.xdelta", SearchOption.AllDirectories)
                                       .OrderBy(f => f)
                                       .ToList();

            if (xdeltaFiles.Count > 0)
            {
                progress.Report((-1, "xdelta 바이너리 패치 적용 중..."));
                log($"  발견된 xdelta 패치 수: {xdeltaFiles.Count}개", LogLevel.Info);

                string unpackedRoot = Path.GetDirectoryName(exefsDir)!;

                foreach (var xdeltaPath in xdeltaFiles)
                {
                    string targetFileName = Path.GetFileNameWithoutExtension(xdeltaPath);
                    string relativePath = Path.GetRelativePath(patchDir, xdeltaPath);
                    string relativeTargetKey = Path.Combine(Path.GetDirectoryName(relativePath) ?? string.Empty, targetFileName);
                    var targetFiles = new List<string>();
                    string absoluteExactPath = Path.Combine(unpackedRoot, relativeTargetKey);

                    if (File.Exists(absoluteExactPath))
                        targetFiles.Add(absoluteExactPath);
                    else
                    {
                        if (!string.IsNullOrEmpty(exefsDir))
                            targetFiles.AddRange(Directory.EnumerateFiles(exefsDir, targetFileName, SearchOption.AllDirectories));

                        if (!string.IsNullOrEmpty(romfsDir))
                            targetFiles.AddRange(Directory.EnumerateFiles(romfsDir, targetFileName, SearchOption.AllDirectories));
                    }

                    if (targetFiles.Count > 0)
                    {
                        foreach (var targetPath in targetFiles.Distinct())
                            ApplyXdeltaToTarget(xdeltaPath, targetPath, unpackedRoot, progress, log);
                    }
                    else
                        log($"  ⚠️ xdelta 대상 원본 파일을 찾을 수 없음: {targetFileName}", LogLevel.Info);
                }
            }
        }
    }

    public static void ApplyDlcPatch(string patchDir, string titleIdStr, string romfsDir, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log)
    {
        if (!Directory.Exists(patchDir))
            return;

        string patchRomfs = Path.Combine(patchDir, "romfs");

        if (Directory.Exists(patchRomfs))
        {
            progress.Report((-1, $"DLC 패치 RomFS 병합 중... ({titleIdStr})"));
            log($"  DLC 패치 RomFS 병합: {patchRomfs}", LogLevel.Info);
            MergeDirectory(patchRomfs, romfsDir);
        }

        var xdeltaFiles = Directory.EnumerateFiles(patchDir, "*.xdelta", SearchOption.AllDirectories)
                                   .OrderBy(f => f)
                                   .ToList();

        if (xdeltaFiles.Count == 0)
            return;

        progress.Report((-1, $"DLC xdelta 패치 적용 중... ({titleIdStr})"));
        log($"  발견된 DLC xdelta 패치 수: {xdeltaFiles.Count}개", LogLevel.Info);

        foreach (var xdeltaPath in xdeltaFiles)
        {
            string targetFileName = Path.GetFileNameWithoutExtension(xdeltaPath);
            var targetFiles = Directory.EnumerateFiles(romfsDir, targetFileName, SearchOption.AllDirectories).ToList();

            if (targetFiles.Count == 0)
            {
                log($"  ⚠️ DLC xdelta 대상 원본 파일을 찾을 수 없음: {targetFileName}", LogLevel.Info);
                continue;
            }

            foreach (var targetPath in targetFiles)
                ApplyXdeltaToTarget(xdeltaPath, targetPath, romfsDir, progress, log, isDlc: true);
        }
    }

    private static void ApplyXdeltaToTarget(string xdeltaPath, string targetPath, string displayRoot, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, bool isDlc = false)
    {
        string displayPath = Path.GetRelativePath(displayRoot, targetPath);
        string prefix = isDlc ? "DLC xdelta" : "xdelta";

        log($"  {prefix} 패치 적용: {Path.GetFileName(xdeltaPath)} ➡️ {displayPath}", LogLevel.Info);

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

            if (File.Exists(tempOutPath))
            {
                File.Delete(targetPath);
                File.Move(tempOutPath, targetPath);
            }
        }
        catch (Exception ex)
        {
            log($"  ❌ {prefix} 패치 실패 ({Path.GetFileName(xdeltaPath)}): {ex.Message}", LogLevel.Error);

            if (File.Exists(tempOutPath)) 
                File.Delete(tempOutPath);
        }
    }

    public static void MergeDirectory(string srcDir, string dstDir)
    {
        Directory.CreateDirectory(dstDir);

        foreach (var file in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".xdelta", StringComparison.OrdinalIgnoreCase))
                continue;

            string rel = Path.GetRelativePath(srcDir, file);
            string dest = Path.Combine(dstDir, rel);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }
}