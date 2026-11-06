using Common.Models;
using RomForge.Core.Models.Switch;
using System.IO;

namespace RomForge.Core.Services.Switch;

public static class OutputFormatPipeline
{
    public static async Task<string?> RunAsync(OutputFormat format, string outputDir, Func<string, Task<string?>> buildNsp, Func<IProgress<ProgressInfo>> nszProgressFactory, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (format != OutputFormat.NSZ)
            return await buildNsp(outputDir);

        string workDir = Path.Combine(outputDir, ".nsz_" + Guid.NewGuid().ToString("N")[..8]);

        Directory.CreateDirectory(workDir);

        try
        {
            string? nspPath = await buildNsp(workDir);

            if (nspPath == null)
                return null;

            var config = AppConfig.Instance.Switch;

            return await NspCompressService.CompressAsync(nspPath, outputDir, config.CompressLevel, config.VerifyCompress, config.UseBlockMode, nszProgressFactory(), (message, level, _) => log(message, level), ct);
        }
        finally
        {
            DeleteDirectory(workDir);
        }
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}