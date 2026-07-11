namespace WiiU.Core.Services;

public sealed class WudDecompressionService
{
    public sealed record Result(bool WasCompressed, long BytesWritten);

    public static Result Decompress(string inputPath, string outputPath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        using var reader = WudReader.Open(inputPath);
        long total = reader.UncompressedSize;
        var outDir = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outDir))
            Directory.CreateDirectory(outDir);

        using var outStream = File.Create(outputPath);
        long written = 0;

        reader.ReadAll((chunk, offset) =>
        {
            ct.ThrowIfCancellationRequested();

            outStream.Write(chunk.Span);

            written += chunk.Length;

            if (total > 0)
                progress?.Report((double)written / total);

        }, chunkSize: 4 * 1024 * 1024);

        return new Result(reader.IsCompressed, written);
    }

    public static Task<Result> DecompressAsync(string inputPath, string outputPath, IProgress<double>? progress = null, CancellationToken ct = default) => Task.Run(() => Decompress(inputPath, outputPath, progress, ct), ct);

    public static (bool IsCompressed, long UncompressedSize) Inspect(string path)
    {
        using var reader = WudReader.Open(path);

        return (reader.IsCompressed, reader.UncompressedSize);
    }
}
