using CHD.Core.Models;
using Common;

namespace CHD.Core.Services;

public class FileConverter : IDisposable
{
    private readonly ChdmanService _chdman;
    private readonly string _compression = "zlib";
    private bool _disposed;

    public string CurrentOutputPath { get; private set; }

    public event EventHandler<(string Message, LogLevel Level)> LogMessage;

    public FileConverter(string compression = "zlib")
    {
        _chdman = new ChdmanService();

        _chdman.ErrorReceived += (s, msg) => Log(msg, LogLevel.Error);
        _compression = compression;

    }

    public async Task<ConversionResult> ConvertFileAsync(string filePath, string? outputDir = null, IProgress<ProgressInfo>? progress = null, string? targetFormat = null, CancellationToken ct = default)
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

            _ => ConversionResult.Fail($"지원하지 않는 형식: {source.Format}")
        };
    }

    private async Task<ConversionResult> ConvertFromChdAsync(ConversionSource source, string? outputDir, IProgress<ProgressInfo>? progress, string? targetFormat = null, CancellationToken ct = default)
    {
        var chdPath = source.PrimaryFile;
        var dir = outputDir ?? Path.GetDirectoryName(chdPath)!;
        var name = Path.GetFileNameWithoutExtension(chdPath);

        Log($"{Path.GetFileName(chdPath)} 변환 시작", LogLevel.Highlight);

        try
        {
            ct.ThrowIfCancellationRequested();

            var info = ChdInfoReader.ReadChdInfo(chdPath);

            if (info.SourceType == ChdSourceType.DVD)
            {
                var isoPath = Utils.GetUniqueFilePath(Path.Combine(dir, name + ".iso"));

                CurrentOutputPath = isoPath;

                Log("ISO 추출 중...");

                bool extracted = await _chdman.ExtractRawAsync(chdPath, isoPath, progress, ct);

                if (!extracted || !File.Exists(isoPath))
                    return ConversionResult.Fail("ISO 추출 실패");

                Log($"추출 완료: {isoPath}", LogLevel.Ok);

                CurrentOutputPath = null;

                progress?.Report(new ProgressInfo { Label = "추출 완료", Percent = 100 });

                return new ConversionResult
                {
                    Success = true,
                    Message = "추출 성공 (ISO)",
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

                Log($"{label} 추출 중...");

                bool extracted = await _chdman.ExtractCdAsync(chdPath, tocPath, progress, ct);

                if (!extracted)
                    return ConversionResult.Fail("CHD 추출 실패");

                ct.ThrowIfCancellationRequested();

                if (!File.Exists(tocPath))
                    return ConversionResult.Fail($"{label} 파일 생성 실패");

                var extractedTrackFiles = asCue ? [.. ConversionSource.ParseBinsFromCue(tocPath)] : ConversionSource.ParseFilesFromGdi(tocPath)
                        .Select(f => Path.IsPathRooted(f) ? f : Path.Combine(dir, f))
                        .ToList();
                var missingTrackFile = extractedTrackFiles.FirstOrDefault(f => !File.Exists(f));

                if (missingTrackFile != null)
                    return ConversionResult.Fail($"트랙 파일 생성 실패: {Path.GetFileName(missingTrackFile)}");

                Log($"{label} 추출 완료 ({extractedTrackFiles.Count}개 트랙 파일)", LogLevel.Ok);

                CurrentOutputPath = null;

                progress?.Report(new ProgressInfo { Label = "추출 완료", Percent = 100 });

                return new ConversionResult
                {
                    Success = true,
                    Message = $"추출 성공 ({label}, {extractedTrackFiles.Count}개 트랙 파일)",
                    OutputFile = tocPath,
                    OutputFiles = [tocPath, .. extractedTrackFiles],
                    VerificationPerformed = false
                };
            }
            else
            {
                var cuePath = Path.Combine(dir, name + ".cue");

                CurrentOutputPath = cuePath;

                Log("BIN/CUE 추출 중...");

                bool extracted = await _chdman.ExtractCdAsync(chdPath, cuePath, progress, ct);

                if (!extracted)
                    return ConversionResult.Fail("CHD 추출 실패");

                ct.ThrowIfCancellationRequested();

                if (!File.Exists(cuePath))
                    return ConversionResult.Fail("CUE 파일 생성 실패");

                var extractedBins = ConversionSource.ParseBinsFromCue(cuePath);
                var missingBin = extractedBins.FirstOrDefault(b => !File.Exists(b));

                if (missingBin != null)
                    return ConversionResult.Fail($"BIN 파일 생성 실패: {Path.GetFileName(missingBin)}");

                Log("BIN/CUE 추출 완료", LogLevel.Ok);

                CurrentOutputPath = null;

                progress?.Report(new ProgressInfo { Label = "추출 완료", Percent = 100 });

                return new ConversionResult
                {
                    Success = true,
                    Message = $"변환 성공 (멀티 트랙 BIN/CUE, {extractedBins.Count}개 BIN)",
                    OutputFile = cuePath,
                    OutputFiles = [cuePath, .. extractedBins],
                    VerificationPerformed = false
                };
            }
        }
        catch (OperationCanceledException)
        {
            Log("변환 취소중...", LogLevel.Error);
            CleanupFiles(CurrentOutputPath);

            CurrentOutputPath = null;

            throw;
        }
        catch (Exception ex)
        {
            CleanupFiles(CurrentOutputPath);

            CurrentOutputPath = null;

            return ConversionResult.Fail($"오류: {ex.Message}");
        }
    }

    private async Task<ConversionResult> ConvertToChdAsync(ConversionSource source, string? outputDir, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var inputPath = source.PrimaryFile;
        var chdPath = Utils.GetUniqueFilePath(Utils.ResolveOutputFilePath(inputPath, ".chd", outputDir));

        chdPath = Utils.GetUniqueFilePath(chdPath);

        Log($"{Path.GetFileName(inputPath)} 압축 시작", LogLevel.Highlight);

        CurrentOutputPath = chdPath;

        try
        {
            ct.ThrowIfCancellationRequested();

            bool success = await _chdman.CreateCdAsync(inputPath, chdPath, string.Empty, progress, ct);

            ct.ThrowIfCancellationRequested();

            if (!success || !File.Exists(chdPath))
                return ConversionResult.Fail("CHD 생성 실패");

            CurrentOutputPath = null;

            long originalSize = 0;
            string extension = Path.GetExtension(inputPath).ToLower();

            if (extension == ".cue")
            {
                var sourceDir = Path.GetDirectoryName(inputPath)!;
                var referencedBins = ConversionSource.ParseBinsFromCue(inputPath);

                foreach (var binName in referencedBins)
                {
                    string binPath = Path.Combine(sourceDir, Path.GetFileName(binName));

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

            Log($"압축률: {Utils.FormatFileSize(originalSize)} → {Utils.FormatFileSize(compressedSize)} ({ratio:F1}%)", LogLevel.Highlight);
            Log($"압축 완료: {chdPath}", LogLevel.Ok);

            var result = new ConversionResult
            {
                Success = true,
                Message = "압축 성공",
                OutputFile = chdPath,
                OutputFiles = [chdPath],
                VerificationPerformed = true
            };

            progress?.Report(new ProgressInfo { Label = "압축 완료", Percent = 100 });

            return result;
        }
        catch (OperationCanceledException)
        {
            Log("압축 취소중...", LogLevel.Error);
            CleanupFiles(chdPath);

            CurrentOutputPath = null;

            throw;
        }
        catch (Exception ex)
        {
            CleanupFiles(chdPath);

            CurrentOutputPath = null;

            return ConversionResult.Fail($"오류: {ex.Message}");
        }
    }

    private async Task<ConversionResult> ConvertIsoChdAsync(ConversionSource source, string? outputDir, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var inputPath = source.PrimaryFile;
        var chdPath = Utils.GetUniqueFilePath(Utils.ResolveOutputFilePath(inputPath, ".chd", outputDir));

        Log($"{Path.GetFileName(inputPath)} 압축 시작", LogLevel.Highlight);

        CurrentOutputPath = chdPath;

        try
        {
            ct.ThrowIfCancellationRequested();

            var rawSectorMode = CdRawSectorDetector.Detect(inputPath);

            bool success = rawSectorMode != CdRawSectorMode.None ? await _chdman.CreateCdAsync(inputPath, chdPath, string.Empty, progress, ct) : await _chdman.CreateDvdAsync(inputPath, chdPath, _compression, progress, ct);

            ct.ThrowIfCancellationRequested();

            if (!success || !File.Exists(chdPath))
                return ConversionResult.Fail("CHD 생성 실패");

            CurrentOutputPath = null;

            long originalSize = new FileInfo(inputPath).Length;
            long compressedSize = new FileInfo(chdPath).Length;
            double ratio = originalSize > 0 ? (compressedSize * 100.0 / originalSize) : 0.0;

            Log($"압축률: {Utils.FormatFileSize(originalSize)} → {Utils.FormatFileSize(compressedSize)} ({ratio:F1}%)", LogLevel.Highlight);
            Log($"압축 완료: {chdPath}", LogLevel.Ok);

            progress?.Report(new ProgressInfo { Label = "압축 완료", Percent = 100 });

            return new ConversionResult
            {
                Success = true,
                Message = "압축 성공",
                OutputFile = chdPath,
                OutputFiles = [chdPath],
                VerificationPerformed = true
            };
        }
        catch (OperationCanceledException)
        {
            Log("압축 취소중...", LogLevel.Error);
            CleanupFiles(chdPath);
            CurrentOutputPath = null;

            throw;
        }
        catch (Exception ex)
        {
            CleanupFiles(chdPath);

            CurrentOutputPath = null;
            return ConversionResult.Fail($"오류: {ex.Message}");
        }
    }

    private void CleanupFiles(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                continue;

            try
            {
                File.Delete(path);
                Log($"임시 파일 삭제: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                Log($"파일 삭제 실패: {Path.GetFileName(path)} - {ex.Message}", LogLevel.Error);
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
        _chdman.Dispose();
    }

    public void CleanupCurrentOutput()
    {
        if (!string.IsNullOrEmpty(CurrentOutputPath))
            CleanupFiles(CurrentOutputPath);

        CurrentOutputPath = null;
    }
}