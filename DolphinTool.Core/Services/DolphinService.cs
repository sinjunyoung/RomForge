using Common;
using DolphinTool.Core.Models;
using DolphinTool.Core.Rvz;
using DolphinTool.Core.Services.GameCube;

namespace DolphinTool.Core.Services;

public class DolphinService
{
    public event EventHandler<(string Message, LogLevel Level)>? LogMessage;
    public event EventHandler<ProgressEventArgs>? ProgressChanged;

    public Task ConvertFileAsync(string inputPath, string format, string outputExtension, int compressionLevel = 18, string? outputDir = null, CancellationToken ct = default)
    {
        format = format.ToLowerInvariant();

        return Task.Run(() =>
        {
            string workType = format switch
            {
                "wii" or "gcm" => "압축",
                "gcz" or "wbfs" or "wia" => "재압축",
                "rvz" => "해제",
                _ => "미지원"
            };

            if (workType == "미지원")
            {
                LogMessage?.Invoke(this, ($"지원하지 않는 포맷은 건너뜁니다: {Path.GetFileName(inputPath)}", LogLevel.Error));
                return;
            }

            var dir = outputDir ?? Path.GetDirectoryName(inputPath)!;
            var name = Path.GetFileNameWithoutExtension(inputPath);
            string outputPath = Path.Combine(dir, $"{name}.{outputExtension}");

            outputPath = Utils.GetUniqueFilePath(outputPath);

            LogMessage?.Invoke(this, ( $"{Path.GetFileName(inputPath)} {workType} 시작", LogLevel.Highlight ));

            int result = format switch
            {
                "gcm" or "gcz" or "wii" or "wbfs" or "wia" =>
                    ConvertIsoToRvz(inputPath, outputPath, compressionLevel, ct),

                "rvz" =>
                    ConvertRvzToIso(inputPath, outputPath, ct),

                _ => -2
            };

            if (result == -1 || ct.IsCancellationRequested)
            {
                LogMessage?.Invoke(this, ( $"{workType} 취소됨: {Path.GetFileName(inputPath)}", LogLevel.Error ));
                throw new OperationCanceledException(ct);
            }

            if (result != 0)
            {
                if (File.Exists(outputPath))
                    try { File.Delete(outputPath); } catch { }

                LogMessage?.Invoke(this, ($"{workType} 실패 (에러 코드: {result})", LogLevel.Error));

                throw new InvalidOperationException($"{workType} 실패 (에러 코드: {result})");
            }

            if (workType == "재압축" || workType == "압축")
            {
                long originalSize = new FileInfo(inputPath).Length;
                long compressedSize = new FileInfo(outputPath).Length;

                LogMessage?.Invoke(this, ( $"압축률: {Utils.FormatFileSize(originalSize)} → {Utils.FormatFileSize(compressedSize)} ({compressedSize * 100.0 / originalSize:F1}%)", LogLevel.Highlight ));
            }
            
            LogMessage?.Invoke(this, ( $"{workType} 완료: {outputPath}", LogLevel.Ok ));
        }, ct);
    }

    private int ConvertToGcz(string inputPath, string outputPath, CancellationToken ct)
    {
        try
        {
            Action<string, string, int, Action<double>?, CancellationToken> convert = inputPath.EndsWith(".rvz", StringComparison.OrdinalIgnoreCase)
                ? RvzToGczConverter.Convert
                : IsoToGczConverter.Convert;

            convert(inputPath, outputPath, GczWriter.DefaultBlockSize,
                p => ProgressChanged?.Invoke(this, new ProgressEventArgs((int)(p * 100))), ct);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, (ex.Message, LogLevel.Error));
            return -3;
        }
    }

    private int ConvertRvzToIso(string inputPath, string outputPath, CancellationToken ct)
    {
        try
        {
            RvzToIsoConverter.Convert(inputPath, outputPath, p => ProgressChanged?.Invoke(this, new ProgressEventArgs((int)(p * 100))), ct);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, (ex.Message, LogLevel.Error));
            return -3;
        }
    }

    private int ConvertIsoToRvz(string inputPath, string outputPath, int compressionLevel, CancellationToken ct)
    {
        try
        {
            IsoToRvzConverter.Convert(inputPath, outputPath, compressionLevel, 131072, p => ProgressChanged?.Invoke(this, new ProgressEventArgs((int)(p * 100))), ct);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, (ex.Message, LogLevel.Error));
            return -3;
        }
    }
}