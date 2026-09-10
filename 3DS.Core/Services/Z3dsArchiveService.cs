using _3DS.Core.Models;
using Common;

namespace _3DS.Core.Services;

public static class Z3dsArchiveService
{
    public static Task<string> CompressAsync(string inputPath, int compressionLevel = 18, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default) => Z3dsCompressor.CompressAsync(inputPath, compressionLevel, progress, log, ct);

    public static Task CompressFromCiaAsync(string inputPath, int compressionLevel = 18, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default) => Z3dsCompressor.CompressFromCiaAsync(inputPath, compressionLevel, progress, log, ct);

    public static Task DecompressAsync(string inputPath, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default) => Z3dsDecompressor.DecompressAsync(inputPath, progress, log, ct);

    public static Z3dsHeader ParseZ3dsHeader(Stream input) => Z3dsFormat.ParseZ3dsHeader(input);

    public static List<SeekEntry> ParseSeekTable(Stream input, long dataStart, long compressedDataLength) => Z3dsFormat.ParseSeekTable(input, dataStart, compressedDataLength);
}