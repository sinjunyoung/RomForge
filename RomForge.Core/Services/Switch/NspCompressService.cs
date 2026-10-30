using Common;

namespace RomForge.Core.Services.Switch;

public static class NspCompressService
{
    public static Task<string> CompressAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
        => ContainerConverter.CompressOrDecompressAsync(OutputContainer.AsNsp, inputPath, outputDir, true, compressionLevel, validation, useBlockMode, progress, log, ct);

    public static Task<string> DecompressAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
        => ContainerConverter.CompressOrDecompressAsync(OutputContainer.AsNsp, inputPath, outputDir, false, 0, false, false, progress, log, ct);
}