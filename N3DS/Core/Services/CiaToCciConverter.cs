using Common.Models;
using Common.Localization;
using Common.Services;
using N3DS.Core.Crypto;

namespace N3DS.Core.Services;

public class CiaToCciConverter(KeyStore keyStore)
{
    private const string OutputExtension = ".cci";

    public async Task ConvertAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
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
                    0x0004000E => Loc.S.ButtonFilterUpdate,
                    0x0004008C => "DLC",
                    _ => Loc.S.ErrUnsupportedContentType(titleType.ToString("X8"))
                };

                throw new NotSupportedException(Loc.S.ErrCannotRestoreCci(typeDescription));
            }

            outputPath = Utils.GetUniqueFilePath(Utils.ResolveOutputFilePath(inputPath, OutputExtension, outputDir));
            
            using var outputStream = File.Open(outputPath, FileMode.Create, FileAccess.ReadWrite);

            log?.Invoke(Loc.S.LogConvertToCciStart(Path.GetFileName(inputPath)), LogLevel.Highlight);

            long totalSize = NcsdBuilder.CalculateOutputSize(ctx);

            if (totalSize <= 0)
                throw new InvalidDataException(Loc.S.ErrInvalidNcsdStructure);

            void reporter(long written, long total)
            {
                progress?.Report(new ProgressInfo { Percent = (int)((double)written / total * 100) });
            }

            await NcsdBuilder.BuildAsync(ctx, outputStream, reporter, ct);

            isCompleted = true;

            log?.Invoke(Loc.S.LogConvertDone(outputPath), LogLevel.Ok);
        }
        finally
        {
            if (!isCompleted && !string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
            {
                try 
                { 
                    File.Delete(outputPath); 
                }
                catch { }
            }
        }
    }
}