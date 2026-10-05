using _3DS.Core.Services;
using CD.Core.Services.Readers;
using CD.Core.Services.Writers;
using CHD.Core.Services;
using Common;
using RomForge.Core.Models.Compression;
using System.IO;
using WiiGC.Core.Services;

namespace RomForge.Core.Services.Patch;

public class CompressKnownConverter(Action<string, LogLevel> log, IProgress<ProgressInfo> progress, int dolphinCompressLevel)
{
    public async Task ConvertAsync(DetectResult detected, string outputPath, string? outputCuePath, List<string> copiedTrackPaths, string? outputCcdPath, string? outputGdiPath, string outputDir, CancellationToken ct)
    {
        switch (detected.Format)
        {
            case RomFormat.Ccd:
                {
                    progress.Report(new ProgressInfo { Label = "BIN/CUE 변환 중...", Percent = 0 });

                    var discReader = DiscImageReaderFactory.Resolve(outputCcdPath!);
                    var discImage = discReader.Read(outputCcdPath!);
                    var tempCuePath = await BinCueWriter.WriteAsync(discImage, outputDir, Path.GetFileNameWithoutExtension(outputPath), progress, ct);
                    var tempBinPath = Path.ChangeExtension(tempCuePath, ".bin");

                    try
                    {
                        progress.Report(new ProgressInfo { Label = "CHD 변환 중...", Percent = 0 });

                        FileConverter converter = new(AppConfig.Instance.Chdman.Compression);
                        converter.LogMessage += (_, e) => log(e.Message, e.Level);

                        var chdResult = await converter.ConvertFileAsync(tempCuePath, null, progress, null, ct);

                        if (!chdResult.Success)
                            throw new Exception($"CHD 변환 실패: {chdResult.Message}");

                        File.Delete(outputPath);
                        File.Delete(outputCcdPath!);

                        var subPath = Path.ChangeExtension(outputCcdPath, ".sub");

                        if (File.Exists(subPath))
                            File.Delete(subPath);
                    }
                    finally
                    {
                        if (File.Exists(tempCuePath))
                            File.Delete(tempCuePath);

                        if (File.Exists(tempBinPath))
                            File.Delete(tempBinPath);
                    }

                    break;
                }
            case RomFormat.Bin:
                {
                    progress.Report(new ProgressInfo { Label = "CHD 변환 중...", Percent = 0 });

                    FileConverter converter = new(AppConfig.Instance.Chdman.Compression);
                    converter.LogMessage += (_, e) => log(e.Message, e.Level);

                    var chdResult = await converter.ConvertFileAsync(outputCuePath!, null, progress, null, ct);

                    if (!chdResult.Success)
                        throw new Exception($"CHD 변환 실패: {chdResult.Message}");

                    File.Delete(outputPath);
                    File.Delete(outputCuePath!);

                    foreach (var trackPath in copiedTrackPaths)
                        if (File.Exists(trackPath))
                            File.Delete(trackPath);

                    copiedTrackPaths.Clear();

                    break;
                }
            case RomFormat.Gdi:
                {
                    progress.Report(new ProgressInfo { Label = "CHD 변환 중...", Percent = 0 });

                    FileConverter converter = new(AppConfig.Instance.Chdman.Compression);
                    converter.LogMessage += (_, e) => log(e.Message, e.Level);

                    var chdResult = await converter.ConvertFileAsync(outputGdiPath!, null, progress, null, ct);

                    if (!chdResult.Success)
                        throw new Exception($"CHD 변환 실패: {chdResult.Message}");

                    File.Delete(outputPath);
                    File.Delete(outputGdiPath!);

                    foreach (var trackPath in copiedTrackPaths)
                        if (File.Exists(trackPath))
                            File.Delete(trackPath);

                    copiedTrackPaths.Clear();

                    break;
                }
            case RomFormat.Iso:
                {
                    progress.Report(new ProgressInfo { Label = "CHD 변환 중...", Percent = 0 });

                    FileConverter converter = new(AppConfig.Instance.Chdman.Compression);
                    converter.LogMessage += (_, e) => log(e.Message, e.Level);

                    var chdResult = await converter.ConvertFileAsync(outputPath, null, progress, null, ct);

                    if (!chdResult.Success)
                        throw new Exception($"CHD 변환 실패: {chdResult.Message}");

                    File.Delete(outputPath);

                    break;
                }
            case RomFormat.Cci:
                {
                    progress.Report(new ProgressInfo { Label = "ZCCI 압축 중...", Percent = 0 });

                    await Z3dsCompressor.CompressAsync(outputPath, AppConfig.Instance.Azahar.CompressLevel, progress, log, null, ct);

                    File.Delete(outputPath);

                    break;
                }
            case RomFormat.Cia:
                {
                    progress.Report(new ProgressInfo { Label = "ZCCI 압축 중...", Percent = 0 });

                    await Z3dsCompressor.CompressFromCiaAsync(outputPath, AppConfig.Instance.Azahar.CompressLevel, progress, log, null, ct);

                    File.Delete(outputPath);

                    break;
                }
            case RomFormat.Gcm:
            case RomFormat.Wii:
            case RomFormat.Wbfs:
                {
                    progress.Report(new ProgressInfo { Label = "포맷 변환 중...", Percent = 0 });

                    DiscImageConverter dolphin = new();
                    dolphin.LogMessage += (_, e) => log(e.Message, e.Level);
                    dolphin.ProgressChanged += (_, e) => progress.Report(new ProgressInfo { Label = "포맷 변환 중...", Percent = e.Progress });

                    await dolphin.ConvertFileAsync(outputPath, detected.Format.ToString(), detected.OutputExtension, dolphinCompressLevel, null, ct);
                    File.Delete(outputPath);

                    break;
                }
        }
    }
}