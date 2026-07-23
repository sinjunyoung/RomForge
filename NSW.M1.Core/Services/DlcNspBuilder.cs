using Common;
using NSW.HacPack.Enums;
using NSW.HacPack.Services;
using System.Diagnostics;
using Path = System.IO.Path;

namespace NSW.M1.Core.Services;

public static class DlcNspBuilder
{
    public static void BuildDlcNsps(BuildRequest req, WorkDirs dirs, NcaGenerationOptions baseSettings, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        string dlcBaseDir = Path.Combine(dirs.Unpacked, "DLCs");

        if (!Directory.Exists(dlcBaseDir))
            return;

        var sw = Stopwatch.StartNew();

        log("━━ 7단계(7/9): DLC 빌드 시작 ━━", LogLevel.Info);

        int dlcCount = 0;

        foreach (var dlcDir in Directory.GetDirectories(dlcBaseDir))
        {
            ct.ThrowIfCancellationRequested();

            string titleIdStr = Path.GetFileName(dlcDir);

            if (!ulong.TryParse(titleIdStr, System.Globalization.NumberStyles.HexNumber, null, out ulong titleId))
                continue;

            string romfsPath = Path.Combine(dlcDir, "romfs");

            if (req.DlcPatchDirs.TryGetValue(titleIdStr, out var dlcPatchDir) && !string.IsNullOrEmpty(dlcPatchDir))
                NspPatchApplier.ApplyDlcPatch(dlcPatchDir, titleIdStr, romfsPath, progress, log);

            bool hasRomfs = Directory.Exists(romfsPath) && Directory.EnumerateFileSystemEntries(romfsPath).Any();

            log($"  DLC 빌드 시도: {titleIdStr}", LogLevel.Info);

            var dlcSettings = new NcaGenerationOptions
            {
                TitleId = titleId,
                TempDirectory = Path.Combine(dirs.Temp, "dlc_" + titleIdStr),
                OutDirectory = baseSettings.OutDirectory,
                RomfsDirectory = hasRomfs ? romfsPath : string.Empty,
                TitleType = LibHac.Ncm.ContentMetaType.AddOnContent,
                NcaType = LibHac.FsSystem.NcaHeader.ContentType.PublicData,
                SdkVersion = req.OverrideSdkVersion ?? baseSettings.SdkVersion,
                KeyGeneration = req.OverrideKeyGeneration ?? baseSettings.KeyGeneration,
                KeySet = baseSettings.KeySet,
                KeyAreaKey = baseSettings.KeyAreaKey,
                NcaSig = NcaSigType.Zero,
                Plaintext = 0
            };

            try
            {
                Directory.CreateDirectory(dlcSettings.TempDirectory);

                if (hasRomfs)
                    dlcSettings.PublicDataNcaPath = NcaGenerator.GenerateRomfsNca(dlcSettings, "DLC", progress, ct) ?? string.Empty;

                NcaGenerator.GenerateMetaNca([dlcSettings], progress, ct);

                log($"  DLC 완료: {titleIdStr}", LogLevel.Ok);
                dlcCount++;
            }
            catch (Exception ex)
            {
                log($"  DLC 실패: {titleIdStr} - {ex.Message}", LogLevel.Error);
            }
            finally
            {
                if (Directory.Exists(dlcSettings.TempDirectory))
                    Directory.Delete(dlcSettings.TempDirectory, true);
            }
        }

        log($"  총 ({dlcCount})개의 DLC 빌드 완료 : ({sw.Elapsed.TotalSeconds:F2}s)", LogLevel.Ok);
    }
}