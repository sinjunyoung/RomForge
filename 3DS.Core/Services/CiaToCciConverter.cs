using _3DS.Core.Crypto;
using Common;

namespace _3DS.Core.Services;

public class CiaToCciConverter(KeyStore keyStore)
{
    private const string OutputExtension = ".cci";

    public async Task ConvertAsync(string inputPath, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, string? outputDir = null, CancellationToken ct = default)
    {
        string? outputPath = null;
        bool isCompleted = false;

        try
        {
            var unpacker = new CiaReader(keyStore);
            await using var ctx = await unpacker.OpenAsync(inputPath, log, ct);
            uint titleType = (uint)(ctx.Ticket.TitleId >> 32);

            if (titleType != 0x00040000)
            {
                string typeDescription = titleType switch
                {
                    0x0004000E => "업데이트",
                    0x0004008C => "DLC",
                    _ => $"미지원 콘텐츠 타입 (Type ID: 0x{titleType:X8})"
                };

                throw new NotSupportedException($"{typeDescription} 파일은 CCI 복원이 불가능합니다. (본편만 가능)");
            }

            outputPath = Utils.GetUniqueFilePath(Utils.ResolveOutputFilePath(inputPath, OutputExtension, outputDir));
            
            using var outputStream = File.Open(outputPath, FileMode.Create, FileAccess.ReadWrite);

            log?.Invoke($"{Path.GetFileName(inputPath)} → CCI 변환 시작", LogLevel.Highlight);

            long totalSize = NcsdBuilder.CalculateOutputSize(ctx);

            if (totalSize <= 0)
                throw new InvalidDataException("유효한 NCSD(카트리지 롬) 구조를 빌드할 수 없습니다.");

            void reporter(long written, long total)
            {
                progress?.Report(new ProgressInfo { Percent = (int)((double)written / total * 100) });
            }

            await NcsdBuilder.BuildAsync(ctx, outputStream, reporter, ct);

            isCompleted = true;

            log?.Invoke($"변환 완료: {outputPath}", LogLevel.Ok);
        }
        finally
        {
            if (!isCompleted && !string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
            {
                try { File.Delete(outputPath); } catch { }
            }
        }
    }
}