using CHD.Core.Services;
using Common.Localization;
using Common.Models;
using Common.Services;
using Patch.Core.Formats.DCP.Services;
using RomForge.Core.Models.Compression;
using System.IO;

namespace RomForge.Core.Services.Patch;

public class Orchestrator(Action<string, LogLevel>? log, IProgress<ProgressInfo>? progress, bool autoCompress, int wiiGcCompressLevel)
{
    private string? _outputCuePath;
    private string? _outputCcdPath;
    private string? _outputGdiPath;
    private List<string> _copiedTrackPaths = [];
    private readonly BinTrackCopier _binTrackCopier = new(log);
    private readonly CcdCompanionCopier _ccdCompanionCopier = new(log);
    private readonly GdiTrackCopier _gdiTrackCopier = new(log);
    private readonly ZipCompressor _zipCompressor = new(log, progress);
    private readonly CompressKnownConverter _compressKnownConverter = new(log, progress, wiiGcCompressLevel);

    public async Task PatchAsync(string? sourcePath, IReadOnlyList<string> patchPaths, DetectResult detected, string outputDir, string outputPath, bool sourceIsTemporary, CancellationToken ct)
    {
        _outputCuePath = null;
        _outputCcdPath = null;
        _outputGdiPath = null;
        _copiedTrackPaths = [];

        Chain.Validate(patchPaths);

        bool isZipTarget = detected.Format is not (RomFormat.Bin or RomFormat.Iso or RomFormat.Gcm or RomFormat.Wii or RomFormat.Wbfs or RomFormat.Ccd or RomFormat.Cci or RomFormat.Cia or RomFormat.Gdi);
        string patchPath = patchPaths[0];
        bool isDcpPatch = Path.GetExtension(patchPath).Equals(".dcp", StringComparison.OrdinalIgnoreCase);
        bool skipCompress;

        if (isDcpPatch)
        {
            if (detected.Format != RomFormat.Gdi)
                throw new InvalidOperationException(Loc.S.ErrDcpGdiSourceOnly);

            string? gdiPath = sourcePath;

            if (!string.Equals(Path.GetExtension(gdiPath), ".gdi", StringComparison.OrdinalIgnoreCase))
            {
                var sourceDir = Path.GetDirectoryName(sourcePath)!;

                gdiPath = Directory.GetFiles(sourceDir, "*.gdi").FirstOrDefault() ?? throw new InvalidOperationException(Loc.S.ErrDcpGdiNotFound);
            }

            string workDir = Path.GetDirectoryName(gdiPath)!;
            string titleName = VersionInfoExtractor.ApplySuffix(Path.GetFileNameWithoutExtension(gdiPath ?? string.Empty), patchPath ?? string.Empty, AppConfig.Instance.Patch.NamingEnabled, AppConfig.Instance.Patch.NamingFormat ?? string.Empty);

            if (sourceIsTemporary)
            {
                await GdRomApplier.ApplyAsync(gdiPath, patchPath, workDir, (p, msg) => progress?.Report(new ProgressInfo { Percent = (int)(p * 100), Label = msg }), msg => log?.Invoke(msg, LogLevel.Info), ct);
                progress?.Report(new ProgressInfo { Label = Loc.S.ProgressPatchDone, Percent = 100 });
                log?.Invoke(Loc.S.LogPatchDoneName(gdiPath), LogLevel.Ok);
                Directory.CreateDirectory(outputDir);

                if (autoCompress)
                {
                    progress?.Report(new ProgressInfo { Label = Loc.S.ProgressConvertingChd, Percent = 0 });

                    FileConverter converter = new(AppConfig.Instance.Chdman.Compression);

                    converter.LogMessage += (_, e) => log?.Invoke(e.Message, e.Level);

                    var chdResult = await converter.ConvertFileAsync(gdiPath, null, progress, null, ct);

                    if (!chdResult.Success)
                        throw new Exception(Loc.S.ErrChdConvertFailed(chdResult.Message));

                    string finalChdPath = Utils.GetUniqueFilePath(Path.Combine(outputDir, titleName + ".chd"));

                    File.Move(chdResult.OutputFile!, finalChdPath);
                    log?.Invoke(Loc.S.LogChdConvertDone(finalChdPath), LogLevel.Ok);
                }
                else
                {
                    string finalDir = Utils.GetUniqueFolderPath(Path.Combine(outputDir, titleName));

                    Directory.Move(workDir, finalDir);
                    log?.Invoke(Loc.S.LogResultSaved(finalDir), LogLevel.Ok);
                }
            }
            else
            {
                string dcpOutputDir = Utils.GetUniqueFolderPath(Path.Combine(outputDir, titleName));

                Directory.CreateDirectory(dcpOutputDir);
                await GdRomApplier.ApplyAsync(gdiPath, patchPath, dcpOutputDir, (p, msg) => progress?.Report(new ProgressInfo { Percent = (int)(p * 100), Label = msg }), msg => log?.Invoke(msg, LogLevel.Info), ct);

                _outputGdiPath = Path.Combine(dcpOutputDir ?? string.Empty, Path.GetFileName(gdiPath ?? string.Empty));

                if (File.Exists(_outputGdiPath))
                {
                    var rebuiltGdi = GdiFile.Parse(_outputGdiPath);

                    foreach (var track in rebuiltGdi.Tracks)
                    {
                        var trackPath = rebuiltGdi.GetTrackFullPath(track);

                        if (File.Exists(trackPath))
                            _copiedTrackPaths.Add(trackPath);
                    }
                }

                progress?.Report(new ProgressInfo { Label = Loc.S.ProgressPatchDone, Percent = 100 });
                log?.Invoke(Loc.S.LogPatchDoneName(_outputGdiPath), LogLevel.Ok);

                if (autoCompress && File.Exists(_outputGdiPath))
                    await _compressKnownConverter.ConvertAsync(detected, outputPath, null, _copiedTrackPaths, null, _outputGdiPath, outputDir, ct);
            }

            return;
        }

        await Chain.ApplyAsync(sourcePath, patchPaths, outputPath, outputDir, log, progress, ct);
        progress?.Report(new ProgressInfo { Label = Loc.S.ProgressPatchDone, Percent = 100 });
        log?.Invoke(Loc.S.LogPatchDoneName(outputPath), LogLevel.Ok);

        skipCompress = false;

        if (detected.Format == RomFormat.Bin)
        {
            _outputCuePath = await Task.Run(() => _binTrackCopier.CopyBinTracksAsync(sourcePath, outputDir, outputPath, _copiedTrackPaths, sourceIsTemporary, progress, ct), ct);
            skipCompress = _outputCuePath is null;
        }
        else if (detected.Format == RomFormat.Ccd)
        {
            _outputCcdPath = await Task.Run(() => _ccdCompanionCopier.CopyCcd(sourcePath, outputPath, sourceIsTemporary), ct);
            skipCompress = _outputCcdPath is null;
        }
        else if (detected.Format == RomFormat.Gdi)
        {
            _outputGdiPath = await Task.Run(() => _gdiTrackCopier.CopyGdiTracks(sourcePath, outputDir, outputPath, _copiedTrackPaths, sourceIsTemporary, progress, ct), ct);
            skipCompress = _outputGdiPath is null;
        }

        if (!autoCompress || skipCompress)
            return;

        if (isZipTarget)
        {
            progress?.Report(new ProgressInfo { Label = Loc.S.LogStatusCompressing, Percent = 0 });
            await _zipCompressor.CompressFromFileAsync(outputPath, outputDir, ct);
        }
        else
            await _compressKnownConverter.ConvertAsync(detected, outputPath, _outputCuePath, _copiedTrackPaths, _outputCcdPath, _outputGdiPath, outputDir, ct);
    }

    public void Cleanup(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            if (_outputCuePath is not null && File.Exists(_outputCuePath))
                File.Delete(_outputCuePath);

            if (_outputCcdPath is not null && File.Exists(_outputCcdPath))
                File.Delete(_outputCcdPath);

            if (_outputGdiPath is not null && File.Exists(_outputGdiPath))
                File.Delete(_outputGdiPath);

            foreach (var trackPath in _copiedTrackPaths)
                if (File.Exists(trackPath))
                    File.Delete(trackPath);
        }
        catch (Exception ex)
        {
            log?.Invoke(Loc.S.LogCleanupFailed(ex.Message), LogLevel.Error);
        }
    }
}