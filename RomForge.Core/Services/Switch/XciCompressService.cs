using Common;

namespace RomForge.Core.Services.Switch;

public static class XciCompressService
{
    public static Task<string> CompressAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
        => ContainerConverter.CompressOrDecompressAsync(OutputContainer.AsXci, inputPath, true, compressionLevel, validation, useBlockMode, progress, log, outputDir, ct);

    public static Task<string> DecompressAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
        => ContainerConverter.CompressOrDecompressAsync(OutputContainer.AsXci, inputPath, false, 0, false, false, progress, log, outputDir, ct);
}