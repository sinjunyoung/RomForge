using CHD.Core.Models;
using Common.Localization;
using Common.Models;
using Common.Services;

namespace CHD.Core.Services;

public class FileConverter : IDisposable
{
    private readonly ChdmanService _chdman;
    private readonly string _compression = "zlib";
    private bool _disposed;

    public string? CurrentOutputPath { get; private set; }

    public event EventHandler<(string Message, LogLevel Level)>? LogMessage;

    public FileConverter(string compression = "zlib")
    {
        _chdman = new ChdmanService();

        _chdman.ErrorReceived += (s, msg) => Log(msg, LogLevel.Error);
        _compression = compression;

    }

    public async Task<ConversionResult> ConvertFileAsync(string? filePath, string? outputDir = null, IProgress<ProgressInfo>? progress = null, string? targetFormat = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var pathValidation = FilenameHandler.ValidatePath(filePath);

        if (!pathValidation.IsValid)
            return ConversionResult.Fail(pathValidation.Message);

        var source = ConversionSource.FromPath(filePath);
        var validationError = source.Validate();

        if (validationError != null)
            return ConversionResult.Fail(validationError);

        return source.Format switch
        {
            InputFormat.Chd => await ConvertFromChdAsync(source, outputDir, progress, targetFormat, ct),
            InputFormat.Iso => await ConvertIsoChdAsync(source, outputDir, progress, ct),
            InputFormat.BinCue => await ConvertToChdAsync(source, outputDir, progress, ct),
            InputFormat.Gdi => await ConvertToChdAsync(source, outputDir, progress, ct),

            _ => ConversionResult.Fail(Loc.S.ErrUnsupportedFormat(source.Format))
        };
    }

    private async Task<ConversionResult> ConvertFromChdAsync(ConversionSource source, string? outputDir, IProgress<ProgressInfo>? progress, string? targetFormat = null, CancellationToken ct = default)
    {
        var chdPath = source.PrimaryFile;
        var dir = outputDir ?? Path.GetDirectoryName(chdPath)!;
        var name = Path.GetFileNameWithoutExtension(chdPath);

        Log(Loc.S.LogConvertStart(Path.GetFileName(chdPath)), LogLevel.Highlight);

        try
        {
            ct.ThrowIfCancellationRequested();

            var info = ChdInfoReader.ReadChdInfo(chdPath);

            if (info.SourceType == ChdSourceType.DVD)
            {
                var isoPath = Utils.GetUniqueFilePath(Path.Combine(dir, name + ".iso"));

                CurrentOutputPath = isoPath;

                Log(Loc.S.ProgressExtractingIso);

                bool extracted = await _chdman.ExtractRawAsync(chdPath, isoPath, progress, ct);

                if (!extracted || !File.Exists(isoPath))
                    return ConversionResult.Fail(Loc.S.ErrIsoExtractFailed);

                Log(Loc.S.LogExtractDone(isoPath), LogLevel.Ok);

                CurrentOutputPath = null;

                progress?.Report(new ProgressInfo { Label = Loc.S.ProgressExtractDone, Percent = 100 });

                return new ConversionResult
                {
                    Success = true,
                    Message = Loc.S.ResultExtractSuccessIso,
                    OutputFile = isoPath,
                    OutputFiles = [isoPath],
                    VerificationPerformed = true
                };
            }
            else if (info.SourceType == ChdSourceType.GdRom)
            {
                bool asCue = string.Equals(targetFormat, "CUE", StringComparison.OrdinalIgnoreCase);
                var label = asCue ? "CUE" : "GDI";
                var tocPath = Path.Combine(dir, name + (asCue ? ".cue" : ".gdi"));

                CurrentOutputPath = tocPath;

                Log(Loc.S.LogExtractingLabel(label));

                bool extracted = await _chdman.ExtractCdAsync(chdPath, tocPath, progress, ct);

                if (!extracted)
                    return ConversionResult.Fail(Loc.S.ErrChdExtractFailed);

                ct.ThrowIfCancellationRequested();

                if (!File.Exists(tocPath))
                    return ConversionResult.Fail(Loc.S.ErrLabelFileCreateFailed(label));

                var extractedTrackFiles = asCue ? [.. ConversionSource.ParseBinsFromCue(tocPath)] : ConversionSource.ParseFilesFromGdi(tocPath)
                        .Select(f => Path.IsPathRooted(f) ? f : Path.Combine(dir, f))
                        .ToList();
                var missingTrackFile = extractedTrackFiles.FirstOrDefault(f => !File.Exists(f));

                if (missingTrackFile != null)
                    return ConversionResult.Fail(Loc.S.ErrTrackFileCreateFailed(Path.GetFileName(missingTrackFile)));

                Log(Loc.S.LogLabelExtractDone(label, extractedTrackFiles.Count), LogLevel.Ok);

                CurrentOutputPath = null;

                progress?.Report(new ProgressInfo { Label = Loc.S.ProgressExtractDone, Percent = 100 });

                return new ConversionResult
                {
                    Success = true,
                    Message = Loc.S.ResultExtractSuccessTracks(label, extractedTrackFiles.Count),
                    OutputFile = tocPath,
                    OutputFiles = [tocPath, .. extractedTrackFiles],
                    VerificationPerformed = false
                };
            }
            else
            {
                var cuePath = Path.Combine(dir, name + ".cue");

                CurrentOutputPath = cuePath;

                Log(Loc.S.LogExtractingBinCue);

                bool extracted = await _chdman.ExtractCdAsync(chdPath, cuePath, progress, ct);

                if (!extracted)
                    return ConversionResult.Fail(Loc.S.ErrChdExtractFailed);

                ct.ThrowIfCancellationRequested();

                if (!File.Exists(cuePath))
                    return ConversionResult.Fail(Loc.S.ErrCueFileCreateFailed);

                var extractedBins = ConversionSource.ParseBinsFromCue(cuePath);
                var missingBin = extractedBins.FirstOrDefault(b => !File.Exists(b));

                if (missingBin != null)
                    return ConversionResult.Fail(Loc.S.ErrBinFileCreateFailed(Path.GetFileName(missingBin)));

                Log(Loc.S.LogBinCueExtractDone, LogLevel.Ok);

                CurrentOutputPath = null;

                progress?.Report(new ProgressInfo { Label = Loc.S.ProgressExtractDone, Percent = 100 });

                return new ConversionResult
                {
                    Success = true,
                    Message = Loc.S.ResultConvertSuccessMultiTrack(extractedBins.Count),
                    OutputFile = cuePath,
                    OutputFiles = [cuePath, .. extractedBins],
                    VerificationPerformed = false
                };
            }
        }
        catch (OperationCanceledException)
        {
            Log(Loc.S.LogCancellingConversion, LogLevel.Error);
            CleanupFiles(CurrentOutputPath);

            CurrentOutputPath = null;

            throw;
        }
        catch (Exception ex)
        {
            CleanupFiles(CurrentOutputPath);

            CurrentOutputPath = null;

            return ConversionResult.Fail(Loc.S.LogError(ex.Message));
        }
    }

    private async Task<ConversionResult> ConvertToChdAsync(ConversionSource source, string? outputDir, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var inputPath = source.PrimaryFile ?? throw new NullReferenceException();
        var chdPath = Utils.GetUniqueFilePath(Utils.ResolveOutputFilePath(inputPath, ".chd", outputDir));

        chdPath = Utils.GetUniqueFilePath(chdPath);

        Log(Loc.S.LogCompressStart(Path.GetFileName(inputPath)), LogLevel.Highlight);

        CurrentOutputPath = chdPath;

        try
        {
            ct.ThrowIfCancellationRequested();

            bool success = await _chdman.CreateCdAsync(inputPath, chdPath, string.Empty, progress, ct);

            ct.ThrowIfCancellationRequested();

            if (!success || !File.Exists(chdPath))
                return ConversionResult.Fail(Loc.S.ErrChdCreateFailed);

            CurrentOutputPath = null;

            long originalSize = 0;
            string extension = Path.GetExtension(inputPath!).ToLower();

            if (extension == ".cue")
            {
                var sourceDir = Path.GetDirectoryName(inputPath)!;
                var referencedBins = ConversionSource.ParseBinsFromCue(inputPath);

                foreach (var binName in referencedBins)
                {
                    string? binPath = Path.Combine(sourceDir, Path.GetFileName(binName));

                    if (File.Exists(binPath))
                        originalSize += new FileInfo(binPath).Length;
                }

                originalSize += new FileInfo(inputPath).Length;
            }
            else if (extension == ".gdi")
            {
                var sourceDir = Path.GetDirectoryName(inputPath)!;
                var referencedFiles = ConversionSource.ParseFilesFromGdi(inputPath);

                foreach (var fileName in referencedFiles)
                {
                    string filePath = Path.Combine(sourceDir, Path.GetFileName(fileName));

                    if (File.Exists(filePath))
                        originalSize += new FileInfo(filePath).Length;
                }

                originalSize += new FileInfo(inputPath).Length;
            }
            else
                originalSize = new FileInfo(inputPath).Length;

            long compressedSize = new FileInfo(chdPath).Length;

            double ratio = originalSize > 0 ? (compressedSize * 100.0 / originalSize) : 0.0;

            Log($"{Loc.S.LogCompressionRatio}: {Utils.FormatFileSize(originalSize)} → {Utils.FormatFileSize(compressedSize)} ({ratio:F1}%)", LogLevel.Highlight);
            Log(Loc.S.LogCompressDone(chdPath), LogLevel.Ok);

            var result = new ConversionResult
            {
                Success = true,
                Message = Loc.S.ResultCompressSuccess,
                OutputFile = chdPath,
                OutputFiles = [chdPath],
                VerificationPerformed = true
            };

            progress?.Report(new ProgressInfo { Label = Loc.S.ProgressCompressDone, Percent = 100 });

            return result;
        }
        catch (OperationCanceledException)
        {
            Log(Loc.S.LogCancellingCompress, LogLevel.Error);
            CleanupFiles(chdPath);

            CurrentOutputPath = null;

            throw;
        }
        catch (Exception ex)
        {
            CleanupFiles(chdPath);

            CurrentOutputPath = null;

            return ConversionResult.Fail(Loc.S.LogError(ex.Message));
        }
    }

    private async Task<ConversionResult> ConvertIsoChdAsync(ConversionSource source, string? outputDir, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var inputPath = source.PrimaryFile ?? throw new NullReferenceException("inputPath");
        var chdPath = Utils.GetUniqueFilePath(Utils.ResolveOutputFilePath(inputPath, ".chd", outputDir)) ?? throw new NullReferenceException("chdPath");

        Log(Loc.S.LogCompressStart(Path.GetFileName(inputPath)), LogLevel.Highlight);

        CurrentOutputPath = chdPath;

        try
        {
            ct.ThrowIfCancellationRequested();

            var rawSectorMode = CdRawSectorDetector.Detect(inputPath);

            bool success = rawSectorMode != CdRawSectorMode.None ? await _chdman.CreateCdAsync(inputPath, chdPath, string.Empty, progress, ct) : await _chdman.CreateDvdAsync(inputPath, chdPath, _compression, progress, ct);

            ct.ThrowIfCancellationRequested();

            if (!success || !File.Exists(chdPath))
                return ConversionResult.Fail(Loc.S.ErrChdCreateFailed);

            CurrentOutputPath = null;

            long originalSize = new FileInfo(inputPath).Length;
            long compressedSize = new FileInfo(chdPath).Length;
            double ratio = originalSize > 0 ? (compressedSize * 100.0 / originalSize) : 0.0;

            Log($"{Loc.S.LogCompressionRatio}: {Utils.FormatFileSize(originalSize)} → {Utils.FormatFileSize(compressedSize)} ({ratio:F1}%)", LogLevel.Highlight);
            Log(Loc.S.LogCompressDone(chdPath), LogLevel.Ok);

            progress?.Report(new ProgressInfo { Label = Loc.S.ProgressCompressDone, Percent = 100 });

            return new ConversionResult
            {
                Success = true,
                Message = Loc.S.ResultCompressSuccess,
                OutputFile = chdPath,
                OutputFiles = [chdPath],
                VerificationPerformed = true
            };
        }
        catch (OperationCanceledException)
        {
            Log(Loc.S.LogCancellingCompress, LogLevel.Error);
            CleanupFiles(chdPath);
            CurrentOutputPath = null;

            throw;
        }
        catch (Exception ex)
        {
            CleanupFiles(chdPath);

            CurrentOutputPath = null;
            return ConversionResult.Fail(Loc.S.LogError(ex.Message));
        }
    }

    private void CleanupFiles(params string?[] paths)
    {
        if (paths == null)
            return;

        foreach (var path in paths)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                continue;

            try
            {
                File.Delete(path);
                Log(Loc.S.LogTempFileDeleted(Path.GetFileName(path)));
            }
            catch (Exception ex)
            {
                Log(Loc.S.LogFileDeleteFailed(Path.GetFileName(path), ex.Message), LogLevel.Error);
            }
        }
    }

    private void Log(string message, LogLevel level = LogLevel.Info) => LogMessage?.Invoke(this, (message, level));

    public static ChdmanInfo GetChdInfo(string chdPath) => ChdmanService.GetChdInfo(chdPath);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
        _chdman.Dispose();
    }

    public void CleanupCurrentOutput()
    {
        if (!string.IsNullOrEmpty(CurrentOutputPath))
            CleanupFiles(CurrentOutputPath);

        CurrentOutputPath = null;
    }
}