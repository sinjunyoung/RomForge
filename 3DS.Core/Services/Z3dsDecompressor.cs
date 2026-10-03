using _3DS.Core.Models;
using Common;
using System.Buffers;

namespace _3DS.Core.Services;

public static class Z3dsDecompressor
{
    private const string DecompressExtension = ".cci";

    public static async Task DecompressAsync(string inputPath, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, string? outputDir = null, CancellationToken ct = default)
    {
        string? outputPath = null;
        bool isCompleted = false;

        try
        {
            using var inputStream = File.Open(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = Z3dsFormat.ParseZ3dsHeader(inputStream);

            outputPath = Utils.GetUniqueFilePath(Utils.ResolveOutputFilePath(inputPath, DecompressExtension, outputDir));            

            using var outputStream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
            long totalSize = header.UncompressedSize;
            long processed = 0;

            log?.Invoke($"{Path.GetFileName(inputPath)} 해제 시작", LogLevel.Highlight);

            long compressedDataOffset = header.HeaderSize + header.MetadataSize;
            long compressedDataLength = header.CompressedSize;

            await DecompressBlocksAsync(inputStream, outputStream, compressedDataOffset, compressedDataLength,
                bytesProcessed =>
                {
                    processed += bytesProcessed;
                    progress?.Report(new ProgressInfo { Percent = (int)((double)processed / totalSize * 100) });
                }, ct);

            isCompleted = true;
            log?.Invoke($"해제 완료: {outputPath}", LogLevel.Ok);
        }
        finally
        {
            if (!isCompleted && !string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                try { File.Delete(outputPath); } catch { }
        }
    }

    private static async Task DecompressBlocksAsync(Stream input, Stream output, long dataOffset, long compressedDataLength, Action<long>? onProgress, CancellationToken ct)
    {
        var seekEntries = Z3dsFormat.ParseSeekTable(input, dataOffset, compressedDataLength);
        long seekTableSize = 4 + 4 + (seekEntries.Count * 8) + 9;
        long totalBlockBytes = seekEntries.Sum(e => (long)e.CompressedSize);
        long expectedDataLength = totalBlockBytes + seekTableSize;

        if (expectedDataLength != compressedDataLength)
            throw new InvalidDataException($"Seek table 경계 불일치: 합산={totalBlockBytes}, 요청={compressedDataLength}");

        input.Position = dataOffset;

        using var decompressor = new ZstdSharp.Decompressor();
        var pool = ArrayPool<byte>.Shared;

        foreach (var entry in seekEntries)
        {
            ct.ThrowIfCancellationRequested();

            byte[] compressedBuf = pool.Rent((int)entry.CompressedSize);
            byte[] decompressedBuf = pool.Rent((int)entry.DecompressedSize);

            try
            {
                await input.ReadExactlyAsync(compressedBuf.AsMemory(0, (int)entry.CompressedSize), ct);

                int unwrapSize = decompressor.Unwrap(compressedBuf.AsSpan(0, (int)entry.CompressedSize), decompressedBuf.AsSpan(0, (int)entry.DecompressedSize));

                await output.WriteAsync(decompressedBuf.AsMemory(0, unwrapSize), ct);

                onProgress?.Invoke(unwrapSize);
            }
            finally
            {
                pool.Return(compressedBuf);
                pool.Return(decompressedBuf);
            }
        }
    }
}