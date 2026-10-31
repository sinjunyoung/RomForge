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
    public static void BuildDlcNsps(BuildRequest req, UnpackResult unpackResult, WorkDirs dirs, NcaGenerationOptions baseSettings, int compressLevel, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var items = CollectItems(req, unpackResult, dirs);

        if (items.Count == 0)
            return;

        var sw = Stopwatch.StartNew();

        log("━━ 7단계(7/9): DLC 빌드 시작 ━━", LogLevel.Info);

        int dlcCount = 0;

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            string titleIdStr = item.TitleIdStr;
            ulong titleId = item.TitleId;
            string romfsPath = item.RomfsPath;

            if (item.RomfsOpener == null && req.DlcPatchDirs.TryGetValue(titleIdStr, out var dlcPatchDir) && !string.IsNullOrEmpty(dlcPatchDir))
            {
                req.DlcPatchPasswords.TryGetValue(titleIdStr, out var dlcPatchPassword);
                NspPatchApplier.ApplyDlcPatch(dlcPatchDir, titleIdStr, romfsPath, progress, log, dlcPatchPassword);
            }

            bool hasRomfs = item.RomfsOpener != null || (Directory.Exists(romfsPath) && Directory.EnumerateFileSystemEntries(romfsPath).Any());
            bool compressRomfs = item.RomfsOpener != null || (req.DlcCompressRomfs.TryGetValue(titleIdStr, out bool requested) ? requested : File.Exists(Path.Combine(item.DlcDir, RomfsCompression.FlagFileName)));

            log($"DLC 빌드 시도: {titleIdStr}{(compressRomfs ? " (Lz4)" : string.Empty)}", LogLevel.Info);

            var dlcSettings = new NcaGenerationOptions
            {
                TitleId = titleId,
                TempDirectory = Path.Combine(dirs.Temp, "dlc_" + titleIdStr),
                OutDirectory = baseSettings.OutDirectory,
                RomfsDirectory = hasRomfs && item.RomfsOpener == null ? romfsPath : string.Empty,
                RomfsImageOpener = item.RomfsOpener,
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

    private static List<DlcItem> CollectItems(BuildRequest req, UnpackResult unpackResult, WorkDirs dirs)
    {
        var items = new List<DlcItem>();

        if (req.StreamRomfs)
        {
            foreach (var dlc in unpackResult.Dlcs.Where(d => d.RomfsOpener != null))
                items.Add(new DlcItem(dlc.TitleId.ToString("X16"), dlc.TitleId, string.Empty, string.Empty, dlc.RomfsOpener));

            return items;
        }

        string dlcBaseDir = Path.Combine(dirs.Unpacked, "DLCs");

        if (!Directory.Exists(dlcBaseDir))
            return items;

        foreach (var dlcDir in Directory.GetDirectories(dlcBaseDir))
        {
            string titleIdStr = Path.GetFileName(dlcDir);

            if (!ulong.TryParse(titleIdStr, System.Globalization.NumberStyles.HexNumber, null, out ulong titleId))
                continue;

            items.Add(new DlcItem(titleIdStr, titleId, dlcDir, Path.Combine(dlcDir, "romfs"), null));
        }

        return items;
    }

    private sealed record DlcItem(string TitleIdStr, ulong TitleId, string DlcDir, string RomfsPath, Func<Stream>? RomfsOpener);
}