using Common.Models;

namespace RomForge.Core.Services.Switch;

public static class NspCompressService
{
    public static Task<string> CompressAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, SwitchCompressionMode compressionMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
        => ContainerConverter.CompressOrDecompressAsync(OutputContainer.AsNsp, inputPath, outputDir, true, compressionLevel, validation, compressionMode, progress, log, ct);

    public static Task<string> DecompressAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
        => ContainerConverter.CompressOrDecompressAsync(OutputContainer.AsNsp, inputPath, outputDir, false, 0, false, SwitchCompressionMode.Blockless, progress, log, ct);
}