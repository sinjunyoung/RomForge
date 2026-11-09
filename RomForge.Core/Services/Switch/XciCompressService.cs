using Common.Models;

namespace RomForge.Core.Services.Switch;

public static class XciCompressService
{
    public static Task<string> CompressAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, SwitchCompressionMode compressionMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
        => ContainerConverter.CompressOrDecompressAsync(OutputContainer.AsXci, inputPath, outputDir, true, compressionLevel, validation, compressionMode, progress, log, ct);

    public static Task<string> DecompressAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
        => ContainerConverter.CompressOrDecompressAsync(OutputContainer.AsXci, inputPath, outputDir, false, 0, false, SwitchCompressionMode.None, progress, log, ct);
}