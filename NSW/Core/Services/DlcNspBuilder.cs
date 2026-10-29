using Common;
using NSW.Core.Models;
using NSW.HacPack.Enums;
using NSW.HacPack.Services;
using System.Diagnostics;
using System.IO;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public static class DlcNspBuilder
{
    public static void BuildDlcNsps(BuildRequest req, WorkDirs dirs, NcaGenerationOptions baseSettings, int compressLevel, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
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
            {
                req.DlcPatchPasswords.TryGetValue(titleIdStr, out var dlcPatchPassword);
                NspPatchApplier.ApplyDlcPatch(dlcPatchDir, titleIdStr, romfsPath, progress, log, dlcPatchPassword);
            }

            bool hasRomfs = Directory.Exists(romfsPath) && Directory.EnumerateFileSystemEntries(romfsPath).Any();
            bool compressRomfs = req.CompressRomfs ?? File.Exists(Path.Combine(dlcDir, RomfsCompression.FlagFileName));

            log($"DLC 빌드 시도: {titleIdStr}{(compressRomfs ? " (Lz4)" : string.Empty)}", LogLevel.Info);

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
                Plaintext = 0,
                CompressRomfs = compressRomfs
            };

            try
            {
                Directory.CreateDirectory(dlcSettings.TempDirectory);

                if (hasRomfs)
                    dlcSettings.PublicDataNcaPath = NcaGenerator.GenerateRomfsNca(dlcSettings, "DLC", compressLevel, progress, ct) ?? string.Empty;

                NcaGenerator.GenerateMetaNca([dlcSettings], progress, ct);

                log($"DLC 완료: {titleIdStr}", LogLevel.Ok);
                dlcCount++;
            }
            catch (Exception ex)
            {
                log($"DLC 실패: {titleIdStr} - {ex.Message}", LogLevel.Error);
            }
            finally
            {
                if (Directory.Exists(dlcSettings.TempDirectory))
                    Directory.Delete(dlcSettings.TempDirectory, true);
            }
        }

        log($"총 ({dlcCount})개의 DLC 빌드 완료 : ({sw.Elapsed.TotalSeconds:F2}s)", LogLevel.Ok);
    }
}