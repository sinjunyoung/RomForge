namespace WiiGC.Core.Services.GameCube;

public static class IsoToGczConverter
{
    private const int ReadStep = 0x100000;

    public static void Convert(string inputPath, string outputPath, int blockSize = GczWriter.DefaultBlockSize, Action<double>? progress = null, CancellationToken ct = default)
    {
        bool succeeded = false;

        try
        {
            using var input = RvzInputSource.Open(inputPath);
            using var output = File.OpenHandle(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None);
            using var writer = new GczWriter(output, input.Length, blockSize, GetSubType(input));
            int step = Math.Max(ReadStep / blockSize, 1) * blockSize;
            byte[] buffer = new byte[step];
            long offset = 0;

            while (offset < input.Length)
            {
                ct.ThrowIfCancellationRequested();

                int length = (int)Math.Min(step, input.Length - offset);

                input.Read(offset, buffer.AsSpan(0, length));
                writer.Append(buffer.AsSpan(0, length));

                offset += length;

                progress?.Invoke((double)offset / input.Length * 0.99);
            }

            writer.Finish();
            progress?.Invoke(1.0);

            succeeded = true;
        }
        finally
        {
            OutputGuard.DeleteIfFailed(outputPath, succeeded);
        }
    }

    internal static uint GetSubType(IRvzInputSource source)
    {
        if (source.Length < DiscHeader.Size)
            return uint.MaxValue;

        Span<byte> header = stackalloc byte[DiscHeader.Size];

        source.Read(0, header);

        return DiscHeader.GczSubType(DiscHeader.Detect(header));
    }
}