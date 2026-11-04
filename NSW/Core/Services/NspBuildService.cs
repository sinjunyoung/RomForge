using Common;
using Common.Models;
using Common.Services;
using LibHac.Common.Keys;
using NSW.Core.Models;
using NSW.Core.Utils;
using NSW.HacPack.Enums;
using NSW.HacPack.Models;
using NSW.HacPack.Services;
using System.Diagnostics;
using System.IO;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public static class NspBuildService
{
    private sealed record ManualSource(string Dir, Func<Stream>? Opener);

    private sealed class TitleContents
    {
        public CnmtContent? Program { get; set; }

        public CnmtContent? Control { get; set; }

        public CnmtContent? HtmlDoc { get; set; }

        public CnmtContent? Legal { get; set; }

        public IEnumerable<CnmtContent> Ordered()
        {
            if (Program.HasValue)
                yield return Program.Value;

            if (Control.HasValue)
                yield return Control.Value;

            if (HtmlDoc.HasValue)
                yield return HtmlDoc.Value;

            if (Legal.HasValue)
                yield return Legal.Value;
        }
    }

    private const byte ContentTypeProgram = 0x1;

    private const byte ContentTypeControl = 0x3;

    private const byte ContentTypeHtmlDoc = 0x4;

    private const byte ContentTypeLegal = 0x5;

    public static async Task<string> Run(BuildRequest req, BuildMode mode, int compressLevel, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        return await RunProcess(req, mode, keySet, compressLevel, progress, log, ct);
    }

    private static async Task<string> RunProcess(BuildRequest req, BuildMode mode, KeySet keySet, int compressLevel, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct = default)
    {
        var dirs = new WorkDirs(req.OutputDir);

        if (mode == BuildMode.UnpackOnly)
            dirs.Prepare();

        UnpackResult unpackResult;

        if (mode == BuildMode.RebuildOnly)
        {
            log("━━ 기존 언팩 데이터 스캔 중... ━━", LogLevel.Highlight);
            unpackResult = UnpackedDirScanner.Scan(dirs.Unpacked, req.OverrideSdkVersion, req.OverrideKeyGeneration);

            ApplyCompressionOverride(req, unpackResult, log);
        }
        else
        {
            unpackResult = await StepUnpack(req, keySet, dirs, progress, log, ct);

            ApplyCompressionOverride(req, unpackResult, log);

            if (mode == BuildMode.UnpackOnly)
            {
                log("━━ 언팩 완료 ━━", LogLevel.Ok);
                unpackResult.ReleaseResources();
                return dirs.Unpacked;
            }
        }

        string? tempNsp = null;
        List<string> createdDirs = [];
        bool completed = false;

        try
        {
            var settingsList = StepBuildSettings(req, unpackResult, keySet, log);

            if (req.OverrideTitleId.HasValue)
            {
                foreach (var s in settingsList)
                    s.TitleId = req.OverrideTitleId.Value;

                unpackResult.TitleId = req.OverrideTitleId.Value;

                log($"Title ID 오버라이드: {req.OverrideTitleId.Value:x16}", LogLevel.Ok);
            }

            if (req.TargetIdOffset.HasValue)
            {
                settingsList = [.. settingsList.Where(s => s.IdOffset == req.TargetIdOffset.Value)];

                if (settingsList.Count == 0)
                    throw new Exception($"IdOffset {req.TargetIdOffset.Value}에 해당하는 타이틀이 없습니다.");

                var s = settingsList[0];
                s.TitleId += req.TargetIdOffset.Value;
                s.IdOffset = 0;
                unpackResult.TitleId = s.TitleId;
            }

            foreach (var settings in settingsList)
            {
                settings.TitleVersion = unpackResult.GameVersion;
                settings.Language = req.Language;
                settings.UserMetadata = req.UserMetadata;
                settings.KeySet = keySet;
            }

            var baseSettings = settingsList.First(s => s.IdOffset == 0);

            createdDirs = FindMissingDirectories(req.OutputDir);

            Directory.CreateDirectory(req.OutputDir);

            tempNsp = Path.Combine(req.OutputDir, $"~repack_{Guid.NewGuid():N}.tmp");

            int maxFiles = settingsList.Count * 4 + DlcNspBuilder.CountItems(req, unpackResult, dirs) * 2 + 1;
            var contents = settingsList.ToDictionary(s => s, _ => new TitleContents());

            using (var package = new NspPackageWriter(new FileStream(tempNsp, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20), maxFiles))
            {
                foreach (var settings in settingsList)
                {
                    StepNpdm(settings, unpackResult, log, ct);

                    contents[settings].Program = StepProgramNca(req, settings, unpackResult, compressLevel, package, progress, log, ct);
                }

                StepManualNcas(settingsList, contents, unpackResult, compressLevel, package, progress, log, ct);

                foreach (var settings in settingsList)
                    contents[settings].Control = StepControlNca(settings, unpackResult, compressLevel, package, progress, log, ct);

                DlcNspBuilder.BuildDlcNsps(req, unpackResult, dirs, baseSettings, compressLevel, package, progress, log, ct);
                StepMetaNca(settingsList, contents, package, progress, log, ct);
                StepComplete(package, progress, log);
            }

            string finalNsp = StepFinalize(req, unpackResult, tempNsp, progress, log);

            tempNsp = null;
            completed = true;

            return finalNsp;
        }
        finally
        {
            DeleteIfExists(tempNsp);
            unpackResult.ReleaseResources();

            if (!completed)
                DeleteCreatedDirectories(createdDirs);
        }
    }

    private static void ApplyCompressionOverride(BuildRequest req, UnpackResult unpackResult, Action<string, LogLevel> log)
    {
        if (!req.CompressRomfs.HasValue)
            return;

        RomfsCompression.Apply(unpackResult, req.CompressRomfs.Value);
        log(req.CompressRomfs.Value ? "RomFS Lz4 압축: 사용" : "RomFS Lz4 압축: 사용 안 함", LogLevel.Ok);
    }

    private static async Task<UnpackResult> StepUnpack(BuildRequest req, KeySet libHacKeySet, WorkDirs dirs, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        log("━━ 1단계(1/9): 언패킹 ━━", LogLevel.Highlight);
        progress.ReportStatus("언패킹 중...", 0);

        var unpacker = new NspUnpacker(libHacKeySet);
        var result = await unpacker.Unpack(req, dirs.Unpacked, progress, ct);

        log($"언패킹 완료 ({sw.Elapsed:mm\\:ss})", LogLevel.Ok);

        return result;
    }

    private static List<NcaGenerationOptions> StepBuildSettings(BuildRequest req, UnpackResult result, KeySet keySet, Action<string, LogLevel> log)
    {
        var sw = Stopwatch.StartNew();

        log("━━ 2단계(2/9): 설정 초기화 ━━", LogLevel.Highlight);

        byte keyGeneration = result.BaseKeyGeneration == 0 ? (byte)1 : result.BaseKeyGeneration;
        int keygenIdx = keyGeneration - 1;

        if (keygenIdx < 0)
            throw new Exception($"유효하지 않은 KeyGeneration입니다: {keyGeneration}");

        var settingsList = new List<NcaGenerationOptions>();
        var allIdOffsets = result.ExefsDirs.Keys
            .Union(result.VirtualParts.Where(p => p.Value.Exefs != null).Select(p => p.Key))
            .OrderBy(k => k);

        foreach (var idOffset in allIdOffsets)
        {
            result.ExefsDirs.TryGetValue(idOffset, out var exefsDir);
            result.RomfsDirs.TryGetValue(idOffset, out var romfsDir);
            result.LogoDirs.TryGetValue(idOffset, out var logoDir);

            var settings = new NcaGenerationOptions
            {
                IdOffset = idOffset,
                SourceIdOffset = idOffset,
                TitleId = result.TitleId,
                ExefsDirectory = exefsDir ?? string.Empty,
                RomfsDirectory = romfsDir ?? string.Empty,
                RomfsImageOpener = result.RomfsOpeners.GetValueOrDefault(idOffset),
                LogoDirectory = logoDir ?? string.Empty,
                BackupDirectory = Path.Combine(req.OutputDir, "backups"),
                Plaintext = 0,
                HasTitleKey = 0,
                NcaSig = NcaSigType.Zero,
                TitleType = LibHac.Ncm.ContentMetaType.Application,
                SdkVersion = req.OverrideSdkVersion ?? result.BaseSdkVersion,
                KeyGeneration = req.OverrideKeyGeneration ?? keyGeneration,
                KeySet = keySet,
                CompressRomfs = result.CompressedRomfs.Contains(idOffset),
            };

            if (keygenIdx >= settings.KeySet.KeyAreaKeys.Length)
                throw new Exception($"유효하지 않은 KeyGeneration입니다: {keyGeneration}");

            settings.KeyAreaKey = settings.KeySet.KeyAreaKeys[keygenIdx][0].DataRo.ToArray();
            settingsList.Add(settings);
        }

        var first = settingsList[0];

        log($"TitleId: {first.TitleId:x16}  KeyGen: {first.KeyGeneration}  SDK: {first.SdkVersionString}", LogLevel.Ok);
        log($"설정 완료 ({sw.Elapsed:mm\\:ss})", LogLevel.Ok);

        return settingsList;
    }

    private static void StepNpdm(NcaGenerationOptions settings, UnpackResult unpackResult, Action<string, LogLevel> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        log($"━━ 3단계(3/9): NPDM 처리 (IdOffset={settings.IdOffset}) ━━", LogLevel.Highlight);

        if (unpackResult.VirtualParts.TryGetValue(settings.SourceIdOffset, out var parts) && parts.Exefs != null)
            NpdmProcessor.PatchNpdmMetadata(settings, parts.Exefs);
        else
            NpdmProcessor.PatchNpdmMetadata(settings);

        log($"NPDM 완료 ({sw.Elapsed:mm\\:ss})", LogLevel.Ok);
    }

    private static CnmtContent StepProgramNca(BuildRequest req, NcaGenerationOptions settings, UnpackResult unpackResult, int compressLevel, NspPackageWriter package, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        log($"━━ 4단계(4/9): Program NCA 생성 (IdOffset={settings.IdOffset}) ━━", LogLevel.Highlight);
        progress.ReportStatus("Program NCA 생성 중...", 0);

        if (req.HasPatch)
            NspPatchApplier.ApplyPatch(req.PatchDir, unpackResult, settings.SourceIdOffset, progress, log, req.PatchPassword);

        if (unpackResult.VirtualParts.TryGetValue(settings.SourceIdOffset, out var parts))
        {
            settings.ExefsFiles = parts.Exefs?.ReadTopLevelFiles();
            settings.LogoFiles = parts.Logo?.ReadTopLevelFiles();
        }

        settings.NcaType = LibHac.FsSystem.NcaHeader.ContentType.Program;

        var (name, hash, size) = package.AddNca(".nca", output => NcaGenerator.GenerateProgramNcaInto(output, settings, compressLevel, null, progress, ct));

        log($"Program NCA: {name} ({sw.Elapsed:mm\\:ss})", LogLevel.Ok);

        return new CnmtContent(ContentTypeProgram, settings.IdOffset, hash, size);
    }

    private static void StepManualNcas(List<NcaGenerationOptions> settingsList, Dictionary<NcaGenerationOptions, TitleContents> contents, UnpackResult unpackResult, int compressLevel, NspPackageWriter package, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        log("━━ 5단계(5/9): Manual NCA 생성 ━━", LogLevel.Highlight);

        foreach (var (idOffset, source) in ManualSources(unpackResult, p => p.HtmlDoc, unpackResult.HtmlDocDirs))
        {
            var settings = settingsList.FirstOrDefault(s => s.SourceIdOffset == idOffset);

            if (settings == null)
                continue;

            string type = idOffset == 0 ? "htmldoc" : $"htmldoc{idOffset}";

            contents[settings].HtmlDoc = BuildManualNca(settings, source, type, ContentTypeHtmlDoc, compressLevel, package, progress, log, ct);
        }

        foreach (var (idOffset, source) in ManualSources(unpackResult, p => p.Legal, unpackResult.LegalDirs))
        {
            var settings = settingsList.FirstOrDefault(s => s.SourceIdOffset == idOffset);

            if (settings == null)
                continue;

            string type = idOffset == 0 ? "legal" : $"legal{idOffset}";

            contents[settings].Legal = BuildManualNca(settings, source, type, ContentTypeLegal, compressLevel, package, progress, log, ct);
        }
    }

    private static IEnumerable<(byte IdOffset, ManualSource Source)> ManualSources(UnpackResult unpackResult, Func<VirtualProgramParts, VirtualRomfs?> select, Dictionary<byte, string> dirs)
    {
        foreach (var (idOffset, parts) in unpackResult.VirtualParts)
        {
            var virtualFiles = select(parts);

            if (virtualFiles != null && virtualFiles.FileCount > 0)
                yield return (idOffset, new ManualSource(string.Empty, virtualFiles.OpenImage));
        }

        foreach (var (idOffset, dir) in dirs)
        {
            if (HasEntries(dir))
                yield return (idOffset, new ManualSource(dir, null));
        }
    }

    private static bool HasEntries(string dir) => Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length > 0;

    private static CnmtContent BuildManualNca(NcaGenerationOptions settings, ManualSource source, string type, byte contentType, int compressLevel, NspPackageWriter package, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var manualSettings = settings.WithRomfs(source.Dir, LibHac.FsSystem.NcaHeader.ContentType.Manual, source.Opener);
        var (name, hash, size) = package.AddNca(".nca", output => NcaGenerator.GenerateRomfsNcaInto(output, manualSettings, compressLevel, null, progress, ct));

        log($"[{type}] NCA 등록: {name}", LogLevel.Ok);

        return new CnmtContent(contentType, settings.IdOffset, hash, size);
    }

    private static CnmtContent? StepControlNca(NcaGenerationOptions settings, UnpackResult unpackResult, int compressLevel, NspPackageWriter package, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        log($"━━ 6단계(6/9): Control NCA 생성 (IdOffset={settings.IdOffset}) ━━", LogLevel.Highlight);
        progress.ReportStatus("Control NCA 생성 중...", 0);

        NcaGenerationOptions controlSettings;

        if (unpackResult.VirtualParts.TryGetValue(settings.SourceIdOffset, out var parts) && parts.Control != null)
        {
            controlSettings = settings.WithRomfs(string.Empty, LibHac.FsSystem.NcaHeader.ContentType.Control, parts.Control.OpenImage);

            NacpProcessor.ProcessControlMetadata(controlSettings, parts.Control);
        }
        else if (unpackResult.ControlDirs.TryGetValue(settings.SourceIdOffset, out var controlDir))
        {
            controlSettings = settings.WithRomfs(controlDir, LibHac.FsSystem.NcaHeader.ContentType.Control);

            NacpProcessor.ProcessControlMetadata(controlSettings);
        }
        else
            return null;

        var (name, hash, size) = package.AddNca(".nca", output => NcaGenerator.GenerateRomfsNcaInto(output, controlSettings, compressLevel, null, progress, ct));

        log($"Control NCA: {name} ({sw.Elapsed:mm\\:ss})", LogLevel.Ok);

        return new CnmtContent(ContentTypeControl, settings.IdOffset, hash, size);
    }

    private static void StepMetaNca(List<NcaGenerationOptions> settingsList, Dictionary<NcaGenerationOptions, TitleContents> contents, NspPackageWriter package, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        log("━━ 8단계(8/9): Meta NCA 생성 ━━", LogLevel.Highlight);
        progress.ReportStatus("Meta NCA 생성 중...", -1);

        var baseSettings = settingsList.First(s => s.IdOffset == 0);
        var records = new List<CnmtContent>();

        foreach (var settings in settingsList)
            records.AddRange(contents[settings].Ordered());

        byte[] cnmt = CnmtGenerator.Build(baseSettings, records);
        var (name, _, _) = package.AddNca(".cnmt.nca", output => NcaGenerator.GenerateMetaNcaInto(output, baseSettings, cnmt, ct));

        log($"Meta NCA: {name} ({sw.Elapsed:mm\\:ss})", LogLevel.Ok);
    }

    private static void StepComplete(NspPackageWriter package, IProgress<ProgressInfo> progress, Action<string, LogLevel> log)
    {
        log("━━ 9단계(9/9): NSP 마무리 ━━", LogLevel.Highlight);
        progress.ReportStatus("NSP 마무리 중...", 0);
        package.Complete();
    }

    private static string StepFinalize(BuildRequest req, UnpackResult unpackResult, string tempNsp, IProgress<ProgressInfo> progress, Action<string, LogLevel> log)
    {
        string suffix = req.HasPatch ? "Patched_Repack.nsp" : "Repack.nsp";
        string displayVersion = string.IsNullOrWhiteSpace(unpackResult.DisplayVersion) ? "1.0.0" : unpackResult.DisplayVersion;
        string fileName = NameBuilder.FileNameBuild(suffix, unpackResult.KrTitle, unpackResult.EnTitle, unpackResult.TitleIdStr.ToUpper(), displayVersion, unpackResult.GameVersion, unpackResult.DlcCount);
        string finalNsp = Common.Services.Utils.GetUniqueFilePath(Path.Combine(req.OutputDir, fileName));

        File.Move(tempNsp, finalNsp);

        progress.ReportStatus("완료", 100);
        log($"출력: {finalNsp}", LogLevel.Ok);

        return finalNsp;
    }

    private static List<string> FindMissingDirectories(string directory)
    {
        var missing = new List<string>();

        for (string? current = Path.GetFullPath(directory); !string.IsNullOrEmpty(current) && !Directory.Exists(current); current = Path.GetDirectoryName(current))
            missing.Add(current);

        return missing;
    }

    private static void DeleteCreatedDirectories(List<string> directories)
    {
        foreach (var directory in directories)
        {
            try
            {
                if (!Directory.Exists(directory) || Directory.EnumerateFileSystemEntries(directory).Any())
                    return;

                Directory.Delete(directory);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"빈 출력 폴더 삭제 실패 ({directory}): {ex.Message}");
                return;
            }
        }
    }

    private static void DeleteIfExists(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"임시 NSP 삭제 실패 ({path}): {ex.Message}");
        }
    }
}