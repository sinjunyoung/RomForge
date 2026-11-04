using Common.Models;
using NSW.Core.Models;
using NSW.HacPack.Enums;
using NSW.HacPack.Models;
using NSW.HacPack.Services;
using System.Diagnostics;
using System.IO;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public static class DlcNspBuilder
{
    private sealed record DlcItem(string TitleIdStr, ulong TitleId, string DlcDir, string RomfsPath, Func<Stream>? RomfsOpener, VirtualRomfs? Virtual, bool SourceCompressed);

    public static int CountItems(BuildRequest req, UnpackResult unpackResult, WorkDirs dirs) => CollectItems(req, unpackResult, dirs).Count;

    public static void BuildDlcNsps(BuildRequest req, UnpackResult unpackResult, WorkDirs dirs, NcaGenerationOptions baseSettings, int compressLevel, NspPackageWriter package, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
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

            if (req.DlcPatchDirs.TryGetValue(titleIdStr, out var dlcPatchDir) && !string.IsNullOrEmpty(dlcPatchDir))
            {
                req.DlcPatchPasswords.TryGetValue(titleIdStr, out var dlcPatchPassword);

                if (item.Virtual != null)
                    NspPatchApplier.ApplyDlcPatch(dlcPatchDir, titleIdStr, item.Virtual, unpackResult.Resources, progress, log, dlcPatchPassword);
                else
                    NspPatchApplier.ApplyDlcPatch(dlcPatchDir, titleIdStr, romfsPath, progress, log, dlcPatchPassword);
            }

            bool hasRomfs = item.RomfsOpener != null || (Directory.Exists(romfsPath) && Directory.EnumerateFileSystemEntries(romfsPath).Any());
            bool compressRomfs = req.DlcCompressRomfs.TryGetValue(titleIdStr, out bool requested) ? requested : item.RomfsOpener != null ? item.SourceCompressed : File.Exists(Path.Combine(item.DlcDir, RomfsCompression.FlagFileName));

            log($"DLC 빌드 시도: {titleIdStr}{(compressRomfs ? " (Lz4)" : string.Empty)}", LogLevel.Info);

            var dlcSettings = new NcaGenerationOptions
            {
                TitleId = titleId,
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

            var checkpoint = package.CreateCheckpoint();

            try
            {
                var contents = new List<CnmtContent>();

                if (hasRomfs)
                {
                    var (_, hash, size) = package.AddNca(".nca", output => NcaGenerator.GenerateRomfsNcaInto(output, dlcSettings, compressLevel, null, progress, ct));

                    contents.Add(new CnmtContent(0x2, 0, hash, size));
                }

                byte[] cnmt = CnmtGenerator.Build(dlcSettings, contents);

                package.AddNca(".cnmt.nca", output => NcaGenerator.GenerateMetaNcaInto(output, dlcSettings, cnmt, ct));
                log($"DLC 완료: {titleIdStr}", LogLevel.Ok);

                dlcCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                package.Rollback(checkpoint);
                log($"DLC 실패: {titleIdStr} - {ex.Message}", LogLevel.Error);
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
                items.Add(new DlcItem(dlc.TitleId.ToString("X16"), dlc.TitleId, string.Empty, string.Empty, dlc.RomfsOpener, dlc.Virtual, dlc.SourceCompressed));

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

            items.Add(new DlcItem(titleIdStr, titleId, dlcDir, Path.Combine(dlcDir, "romfs"), null, null, false));
        }

        return items;
    }
}