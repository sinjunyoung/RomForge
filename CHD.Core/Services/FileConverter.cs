using CHD.Core.Models;
using CHD.Core.Models.Enums;
using Common;
using System.Text.RegularExpressions;

namespace CHD.Core.Services;

public class FileConverter : IDisposable
{
    private readonly ChdmanService _chdman;
    private readonly string _compression = "zlib";
    private bool _disposed;

    public string CurrentOutputPath { get; private set; }

    public event EventHandler<(string Message, LogLevel Level)> LogMessage;

    private static readonly Regex TrackMode1Regex = new(@"^TRACK\s+\d+\s+MODE1", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public FileConverter(string compression = "zlib")
    {
        _chdman = new ChdmanService();
        _chdman.ErrorReceived += (s, msg) => Log(msg, LogLevel.Error);
        _compression = compression;

    }

    public async Task<ConversionResult> ConvertFileAsync(string filePath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
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
            InputFormat.Chd => await ConvertFromChdAsync(source, progress, ct),
            InputFormat.Iso => await ConvertIsoChdAsync(source, progress, ct),
            InputFormat.BinCue => await ConvertToChdAsync(source, progress, ct),
            InputFormat.Gdi => await ConvertToChdAsync(source, progress, ct),

            _ => ConversionResult.Fail($"지원하지 않는 형식: {source.Format}")
        };
    }

    private async Task<ConversionResult> ConvertFromChdAsync(ConversionSource source, IProgress<ProgressInfo>? progress, CancellationToken cancellationToken)
    {
        var chdPath = source.PrimaryFile;
        var dir = Path.GetDirectoryName(chdPath)!;
        var name = Path.GetFileNameWithoutExtension(chdPath);

        Log($"{Path.GetFileName(chdPath)} 변환 시작", LogLevel.Highlight);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = ChdInfoReader.ReadChdInfo(chdPath);

            if (info.SourceType == ChdSourceType.DVD)
            {
                var isoPath = Utils.GetUniqueFilePath(Path.Combine(dir, name + ".iso"));

                CurrentOutputPath = isoPath;

                Log("ISO 추출 중...");

                bool extracted = await _chdman.ExtractRawAsync(chdPath, isoPath, progress, cancellationToken);

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
            else
            {
                var cuePath = Path.Combine(dir, name + ".cue");
                CurrentOutputPath = cuePath;

                Log("BIN/CUE 추출 중...");

                bool extracted = await _chdman.ExtractCdAsync(chdPath, cuePath, progress, cancellationToken);

                if (!extracted)
                    return ConversionResult.Fail("CHD 추출 실패");

                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(cuePath))
                    return ConversionResult.Fail("CUE 파일 생성 실패");

                var extractedBins = ConversionSource.ParseBinsFromCue(cuePath);
                var missingBin = extractedBins.FirstOrDefault(b => !File.Exists(b));

                if (missingBin != null)
                    return ConversionResult.Fail($"BIN 파일 생성 실패: {Path.GetFileName(missingBin)}");

                Log("BIN/CUE 추출 완료", LogLevel.Ok);

                CurrentOutputPath = null;

                var result = extractedBins.Count == 1 && IsSingleTrackMode1(cuePath) ? ProduceSingleTrackIso(cuePath, extractedBins[0]) : ProduceMultiTrackBinCue(cuePath, extractedBins);

                progress?.Report(new ProgressInfo { Label = "추출 완료", Percent = 100 });

                return result;
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

    private ConversionResult ProduceSingleTrackIso(string cuePath, string binPath)
    {
        Log("단일 트랙 MODE1 감지 - BIN → ISO 변환", LogLevel.Highlight);

        string isoPath = Path.ChangeExtension(binPath, ".iso");

        isoPath = Utils.GetUniqueFilePath(isoPath);

        if (File.Exists(isoPath))
            File.Delete(isoPath);

        File.Move(binPath, isoPath);

        if (File.Exists(cuePath))
            File.Delete(cuePath);

        Log($"변환 완료: {isoPath}", LogLevel.Ok);

        return new ConversionResult
        {
            Success = true,
            Message = "변환 성공 (단일 트랙 ISO)",
            OutputFile = isoPath,
            OutputFiles = [isoPath],
            VerificationPerformed = true
        };
    }

    private ConversionResult ProduceMultiTrackBinCue(string cuePath, IReadOnlyList<string> binFiles)
    {
        Log($"멀티 트랙 감지 ({binFiles.Count}개 BIN) - BIN/CUE 쌍 유지", LogLevel.Highlight);
        Log($"변환 완료: {cuePath}", LogLevel.Ok);

        return new ConversionResult
        {
            Success = true,
            Message = $"변환 성공 (멀티 트랙 BIN/CUE, {binFiles.Count}개 BIN)",
            OutputFile = cuePath,
            OutputFiles = [cuePath, .. binFiles],
            VerificationPerformed = false
        };
    }

    private async Task<ConversionResult> ConvertToChdAsync(ConversionSource source, IProgress<ProgressInfo>? progress, CancellationToken cancellationToken)
    {
        var inputPath = source.PrimaryFile;
        var chdPath = Path.ChangeExtension(inputPath, ".chd");
        chdPath = Utils.GetUniqueFilePath(chdPath);

        Log($"{Path.GetFileName(inputPath)} 압축 시작", LogLevel.Highlight);

        CurrentOutputPath = chdPath;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool success = await _chdman.CreateCdAsync(inputPath, chdPath, progress, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

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

    private async Task<ConversionResult> ConvertIsoChdAsync(ConversionSource source, IProgress<ProgressInfo>? progress, CancellationToken cancellationToken)
    {
        var inputPath = source.PrimaryFile;
        var chdPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, ".chd"));

        Log($"{Path.GetFileName(inputPath)} 압축 시작", LogLevel.Highlight);
        CurrentOutputPath = chdPath;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool success = await _chdman.CreateDvdAsync(inputPath, chdPath, _compression, progress, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

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

    private static bool IsSingleTrackMode1(string cuePath)
    {
        try
        {
            if (!File.Exists(cuePath))
                return false;

            var lines = File.ReadAllLines(cuePath);

            var trackLines = lines
                .Select(l => l.Trim())
                .Where(l => TrackMode1Regex.IsMatch(l))
                .ToList();

            var totalTrackCount = lines
                .Select(l => l.Trim())
                .Count(l => l.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase));

            return totalTrackCount == 1 && trackLines.Count == 1;
        }
        catch
        {
            return false;
        }
    }

    private void CleanupExtractedFiles(string cuePath)
    {
        try
        {
            if (!File.Exists(cuePath))
                return;

            var bins = ConversionSource.ParseBinsFromCue(cuePath);
            CleanupFiles([cuePath, .. bins]);
        }
        catch
        {
            CleanupFiles(cuePath);
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