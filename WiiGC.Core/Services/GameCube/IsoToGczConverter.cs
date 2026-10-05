using System.Buffers.Binary;

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
            DeleteIfFailed(outputPath, succeeded);
        }
    }

    internal static uint GetSubType(IRvzInputSource source)
    {
        if (source.Length < 0x20)
            return uint.MaxValue;

        Span<byte> header = stackalloc byte[0x20];

        source.Read(0, header);

        if (BinaryPrimitives.ReadUInt32BigEndian(header[0x18..]) == 0x5D1C9EA3)
            return 1;

        if (BinaryPrimitives.ReadUInt32BigEndian(header[0x1C..]) == 0xC2339F3D)
            return 0;

        return uint.MaxValue;
    }

    internal static void DeleteIfFailed(string path, bool succeeded)
    {
        if (succeeded)
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}