using Microsoft.Win32.SafeHandles;

namespace WiiGC.Core.Services.GameCube;

public static class GczToIsoConverter
{
    private const int ChunkTarget = 0x100000;

    private readonly record struct Result(byte[]? Buffer, int Length, long Offset);

    public static void Convert(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        bool succeeded = false;

        try
        {
            using var input = RvzInputSource.Open(inputPath);

            if (input is not GczSource source)
                throw new InvalidDataException("GCZ 파일이 아닙니다.");

            using var output = SparseFile.Create(outputPath, source.Length);

            Run(source, output, progress, ct);
            progress?.Invoke(1.0);

            succeeded = true;
        }
        finally
        {
            OutputGuard.DeleteIfFailed(outputPath, succeeded);
        }
    }

    private static void Run(GczSource source, SafeFileHandle output, Action<double>? progress, CancellationToken ct)
    {
        long length = source.Length;
        int chunk = Math.Max(ChunkTarget / source.BlockSize, 1) * source.BlockSize;
        int window = Math.Clamp(Environment.ProcessorCount * 2, 2, 32);
        long written = 0;

        using var pipeline = new OrderedPipeline<byte[], Result, int>(
            window,
            () => new byte[chunk],
            (result, _) =>
            {
                if (result.Buffer != null)
                    RandomAccess.Write(output, result.Buffer.AsSpan(0, result.Length), result.Offset);

                written += result.Length;

                progress?.Invoke(Math.Min(1.0, (double)written / length) * 0.99);
            },
            ct);

        for (long offset = 0; offset < length; offset += chunk)
        {
            long start = offset;
            int size = (int)Math.Min(chunk, length - offset);

            pipeline.Submit(buffer =>
            {
                var span = buffer.AsSpan(0, size);

                source.Read(start, span);

                return span.IndexOfAnyExcept((byte)0) < 0 ? new Result(null, size, start) : new Result(buffer, size, start);
            });
        }

        pipeline.Drain();
    }
}
