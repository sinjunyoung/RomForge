using Common;
using LibHac.Common.Keys;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Core.Enums;
using NSW.HacPack.Enums;
using NSW.HacPack.Services;
using NSW.M1.Core.Models;
using NSW.Utils;
using Patch.Core.Services;
using System.Diagnostics;
using Path = System.IO.Path;

namespace NSW.M1.Core.Services;

public static class NspBuildService
{
    public static async Task<string> Run(BuildRequest req, BuildMode mode, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet;

        return await RunProcess(req, mode, keySet, progress, log, ct);
    }

    private static async Task<string> RunProcess(BuildRequest req, BuildMode mode, KeySet keySet, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, CancellationToken ct = default)
    {
        var dirs = new WorkDirs(req.OutputDir);

        if (mode != BuildMode.RebuildOnly)
            dirs.Prepare();
        else
        {
            if (Directory.Exists(dirs.BuildNca))
                Directory.Delete(dirs.BuildNca, true);

            Directory.CreateDirectory(dirs.BuildNca);
        }

        UnpackResult unpackResult;

        if (mode == BuildMode.RebuildOnly)
        {
            log("━━ 기존 언팩 데이터 스캔 중... ━━", LogLevel.Highlight);
            unpackResult = UnpackedDirScanner.Scan(dirs.Unpacked, req.OverrideSdkVersion, req.OverrideKeyGeneration);
        }
        else
        {
            unpackResult = await StepUnpack(req, keySet, dirs, progress, log, ct);

            if (mode == BuildMode.UnpackOnly)
            {
                log("━━ 언팩 완료 ━━", LogLevel.Ok);

                return dirs.Unpacked;
            }
        }

        var settingsList = StepBuildSettings(req, unpackResult, keySet, dirs, log);

        if (req.OverrideTitleId.HasValue)
        {
            foreach (var s in settingsList)
                s.TitleId = req.OverrideTitleId.Value + s.IdOffset;

            unpackResult.TitleId = req.OverrideTitleId.Value;

            log($"  Title ID 오버라이드: {req.OverrideTitleId.Value:x16}", LogLevel.Ok);
        }

        if (req.TargetIdOffset.HasValue)
        {
            settingsList = [.. settingsList.Where(s => s.IdOffset == req.TargetIdOffset.Value)];

            if (settingsList.Count == 0)
                throw new Exception($"IdOffset {req.TargetIdOffset.Value}에 해당하는 타이틀이 없습니다.");

            var s = settingsList[0];
            s.TitleId += req.TargetIdOffset.Value;
            s.IdOffset = 0;
        }

        foreach (var settings in settingsList)
        {
            settings.TitleVersion = unpackResult.GameVersion;
            settings.Language = req.Language;
            settings.UserMetadata = req.UserMetadata;
            settings.KeySet = keySet;
        }

        var baseSettings = settingsList.First(s => s.IdOffset == 0);

        foreach (var settings in settingsList)
        {
            if (unpackResult.RawProgramNcaPaths.TryGetValue(settings.IdOffset, out var rawPath))
            {
                byte[] hash;
                using (var fs = File.OpenRead(rawPath))
                    hash = System.Security.Cryptography.SHA256.HashData(fs);

                string realNcaId = Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
                string dest = Path.Combine(dirs.BuildNca, $"{realNcaId}.nca");

                File.Copy(rawPath, dest, overwrite: true);
                settings.ProgramNcaPath = dest;
            }
            else
            {
                StepNpdm(settings, log, ct);
                StepProgramNca(req, settings, unpackResult, progress, log, ct);
            }
        }

        StepManualNcas(settingsList, unpackResult, progress, log, ct);

        foreach (var settings in settingsList)
            StepControlNca(settings, unpackResult, progress, log, ct);

        DlcNspBuilder.BuildDlcNsps(req, dirs, baseSettings, progress, log, ct);
        StepMetaNca(settingsList, progress, log, ct);

        return StepPackage(req, baseSettings, unpackResult, progress, log, ct);
    }

    private static async Task<UnpackResult> StepUnpack(BuildRequest req, KeySet libHacKeySet, WorkDirs dirs, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        log("━━ 1단계(1/9): 언패킹 ━━", LogLevel.Highlight);
        progress.Report((0, "언패킹 중..."));

        var unpacker = new NspUnpacker(libHacKeySet);
        var result = await unpacker.Unpack(req, dirs.Unpacked, progress, ct);

        log($"  언패킹 완료 ({sw.Elapsed.TotalSeconds:F2}s)", LogLevel.Ok);

        return result;
    }

    private static List<NcaGenerationOptions> StepBuildSettings(BuildRequest req, UnpackResult result, KeySet keySet, WorkDirs dirs, Action<string, LogLevel> log)
    {
        var sw = Stopwatch.StartNew();

        log("━━ 2단계(2/9): 설정 초기화 ━━", LogLevel.Highlight);

        byte keyGeneration = result.BaseKeyGeneration == 0 ? (byte)1 : result.BaseKeyGeneration;
        int keygenIdx = keyGeneration - 1;

        if (keygenIdx < 0)
            throw new Exception($"유효하지 않은 KeyGeneration입니다: {keyGeneration}");

        var settingsList = new List<NcaGenerationOptions>();

        var allIdOffsets = result.ExefsDirs.Keys
            .Union(result.RawProgramNcaPaths.Keys)
            .OrderBy(k => k);

        foreach (var idOffset in allIdOffsets)
        {
            result.ExefsDirs.TryGetValue(idOffset, out var exefsDir);
            result.RomfsDirs.TryGetValue(idOffset, out var romfsDir);
            result.LogoDirs.TryGetValue(idOffset, out var logoDir);

            var settings = new NcaGenerationOptions
            {
                IdOffset = idOffset,
                TitleId = result.TitleId,
                TempDirectory = dirs.Temp,
                OutDirectory = dirs.BuildNca,
                ExefsDirectory = exefsDir ?? string.Empty,
                RomfsDirectory = romfsDir ?? string.Empty,
                LogoDirectory = logoDir ?? string.Empty,
                BackupDirectory = Path.Combine(Path.GetDirectoryName(dirs.Temp)!, "backups"),
                Plaintext = 0,
                HasTitleKey = 0,
                NcaSig = NcaSigType.Zero,
                TitleType = LibHac.Ncm.ContentMetaType.Application,
                SdkVersion = req.OverrideSdkVersion ?? result.BaseSdkVersion,
                KeyGeneration = req.OverrideKeyGeneration ?? keyGeneration,
                KeySet = keySet
            };

            if (keygenIdx >= settings.KeySet.KeyAreaKeys.Length)
                throw new Exception($"유효하지 않은 KeyGeneration입니다: {keyGeneration}");

            settings.KeyAreaKey = settings.KeySet.KeyAreaKeys[keygenIdx][0].DataRo.ToArray();
            settingsList.Add(settings);
        }

        var first = settingsList[0];

        log($"  TitleId: {first.TitleId:x16}  KeyGen: {first.KeyGeneration}  SDK: {first.SdkVersionString}", LogLevel.Ok);
        log($"  설정 완료 ({sw.Elapsed.TotalSeconds:F2}s)", LogLevel.Ok);

        return settingsList;
    }

    private static void StepNpdm(NcaGenerationOptions settings, Action<string, LogLevel> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        log($"━━ 3단계(3/9): NPDM 처리 (IdOffset={settings.IdOffset}) ━━", LogLevel.Highlight);

        NpdmProcessor.PatchNpdmMetadata(settings);
        log($"  NPDM 완료 ({sw.Elapsed.TotalSeconds:F2}s)", LogLevel.Ok);
    }

    private static void StepProgramNca(BuildRequest req, NcaGenerationOptions settings, UnpackResult unpackResult, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        log("━━ 4단계(4/9): Program NCA 생성 ━━", LogLevel.Highlight);
        progress.Report((0, "Program NCA 생성 중..."));

        if (req.HasPatch)
            NspPatchApplier.ApplyPatch(req.PatchDir, unpackResult, progress, log, req.PatchPassword);

        settings.NcaType = LibHac.FsSystem.NcaHeader.ContentType.Program;
        settings.ProgramNcaPath = NcaGenerator.GenerateProgramNca(settings, progress, ct) ?? string.Empty;

        log($"  Program NCA: {Path.GetFileName(settings.ProgramNcaPath)} ({sw.Elapsed.TotalSeconds}s)", LogLevel.Ok);
    }

    private static void StepManualNcas(List<NcaGenerationOptions> settingsList, UnpackResult unpackResult, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        log("━━ 5단계(5/9): Manual NCA 생성 ━━", LogLevel.Highlight);

        foreach (var (idOffset, htmlDir) in unpackResult.HtmlDocDirs)
        {
            if (!Directory.Exists(htmlDir) || Directory.GetFileSystemEntries(htmlDir).Length == 0)
                continue;

            var settings = settingsList.FirstOrDefault(s => s.IdOffset == idOffset) ?? settingsList[0];
            string type = idOffset == 0 ? "htmldoc" : $"htmldoc{idOffset}";

            log($"  [{type}] 매뉴얼 빌드 시작...", LogLevel.Info);

            var manualSettings = settings.WithRomfs(htmlDir, LibHac.FsSystem.NcaHeader.ContentType.Manual);
            var currentNca = NcaGenerator.GenerateRomfsNca(manualSettings, "Manual", progress, ct);

            if (currentNca == null)
                continue;

            settings.ManualNcaPaths.Add(currentNca);
            settings.HtmlDocNcaPath = currentNca;
            log($"  [{type}] NCA 등록: {Path.GetFileName(currentNca)}", LogLevel.Ok);
        }

        foreach (var (idOffset, legalDir) in unpackResult.LegalDirs)
        {
            if (!Directory.Exists(legalDir) || Directory.GetFileSystemEntries(legalDir).Length == 0)
                continue;

            var settings = settingsList.FirstOrDefault(s => s.IdOffset == idOffset) ?? settingsList[0];
            string type = idOffset == 0 ? "legal" : $"legal{idOffset}";

            log($"  [{type}] 매뉴얼 빌드 시작...", LogLevel.Info);

            var manualSettings = settings.WithRomfs(legalDir, LibHac.FsSystem.NcaHeader.ContentType.Manual);
            var currentNca = NcaGenerator.GenerateRomfsNca(manualSettings, "Manual", progress, ct);

            if (currentNca == null)
                continue;

            settings.ManualNcaPaths.Add(currentNca);
            settings.LegalNcaPath = currentNca;
            log($"  [{type}] NCA 등록: {Path.GetFileName(currentNca)}", LogLevel.Ok);
        }
    }

    private static void StepControlNca(NcaGenerationOptions settings, UnpackResult unpackResult, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        log($"━━ 6단계(6/9): Control NCA 생성 (IdOffset={settings.IdOffset}) ━━", LogLevel.Highlight);
        progress.Report((0, "Control NCA 생성 중..."));

        if (!unpackResult.ControlDirs.TryGetValue(settings.IdOffset, out var controlDir))
            return;

        var controlSettings = settings.WithRomfs(controlDir, LibHac.FsSystem.NcaHeader.ContentType.Control);

        NacpProcessor.ProcessControlMetadata(controlSettings);

        settings.ControlNcaPath = NcaGenerator.GenerateRomfsNca(controlSettings, "Control", progress, ct) ?? string.Empty;

        log($"  Control NCA: {Path.GetFileName(settings.ControlNcaPath)} ({sw.Elapsed.TotalSeconds:F2}s)", LogLevel.Ok);
    }

    private static void StepMetaNca(List<NcaGenerationOptions> settingsList, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        log("━━ 8단계(8/9): Meta NCA 생성 ━━", LogLevel.Highlight);
        progress.Report((-1, "Meta NCA 생성 중..."));

        var baseSettings = settingsList.First(s => s.IdOffset == 0);

        NcaGenerator.GenerateMetaNca(settingsList, progress, ct);

        var metaFiles = Directory.GetFiles(baseSettings.OutDirectory, "*.cnmt.nca")
                                 .OrderByDescending(File.GetLastWriteTime);

        foreach (var file in metaFiles)
        {
            try
            {
                using var fs = File.OpenRead(file);
                var nca = new Nca(baseSettings.KeySet, fs.AsStorage());

                if (nca.Header.ContentType == NcaContentType.Meta)
                {
                    baseSettings.MetaNcaPath = file;
                    break;
                }
            }
            catch
            {
                continue;
            }
        }

        if (string.IsNullOrEmpty(baseSettings.MetaNcaPath))
            log("  Meta NCA를 찾을 수 없습니다!", LogLevel.Error);
        else
            log($"  Meta NCA: {Path.GetFileName(baseSettings.MetaNcaPath)} ({sw.Elapsed.TotalSeconds:F2}s)", LogLevel.Ok);
    }

    private static string StepPackage(BuildRequest req, NcaGenerationOptions settings, UnpackResult unpackResult, IProgress<(int pct, string label)> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        log("━━ 9단계(9/9): NSP 패키징 ━━", LogLevel.Highlight);
        progress.Report((0, "NSP 패키징 중..."));

        Directory.CreateDirectory(req.OutputDir);

        string suffix = req.HasPatch ? "Patched_Repack.nsp" : "Repack.nsp";
        string displayVersion = string.IsNullOrWhiteSpace(unpackResult.DisplayVersion) ? "1.0.0" : unpackResult.DisplayVersion;
        string fileName = NspNameBuilder.FileNameBuild(suffix, unpackResult.KrTitle, unpackResult.EnTitle, unpackResult.TitleIdStr.ToUpper(), displayVersion, unpackResult.GameVersion, unpackResult.DlcCount);
        string finalNsp = Path.Combine(req.OutputDir, fileName);

        finalNsp = Common.Utils.GetUniqueFilePath(finalNsp);

        var fileStreams = new List<(string, Stream)>();

        try
        {
            foreach (var f in Directory.GetFiles(settings.OutDirectory).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
                fileStreams.Add((Path.GetFileName(f), File.OpenRead(f)));

            using var outStream = File.Open(finalNsp, FileMode.Create, FileAccess.Write);

            Pfs0Builder.BuildFromMemoryStreams(fileStreams, outStream, progress, ct);
        }
        finally
        {
            foreach (var (_, stream) in fileStreams)
                stream.Dispose();

            var dirs = new WorkDirs(req.OutputDir);

            if (Directory.Exists(dirs.BuildNca))
                Directory.Delete(dirs.BuildNca, true);

            if (Directory.Exists(dirs.Temp))
                Directory.Delete(dirs.Temp, true);
        }

        progress.Report((100, "완료"));
        log($"  출력: {finalNsp} ({sw.Elapsed.TotalSeconds:F2}s)", LogLevel.Ok);

        return finalNsp;
    }
}