using K4os.Compression.LZ4;

namespace NSW.HacPack.Services;

public static class Lz4BlockMeter
{
    [ThreadStatic]
    private static byte[]? t_target;

    public static int MeasureStoredSize(byte[] buffer, int length, int compressLevel)
    {
        byte[] target = t_target ??= new byte[CompressedRomfsWriter.ChunkSize];
        int stored = 0;

        for (int offset = 0; offset < length; offset += CompressedRomfsWriter.ChunkSize)
        {
            int blockLength = Math.Min(CompressedRomfsWriter.ChunkSize, length - offset);
            int size = LZ4Codec.Encode(buffer.AsSpan(offset, blockLength), target.AsSpan(0, blockLength), (LZ4Level)compressLevel);

            stored += size > 0 && size <= blockLength ? size : blockLength;
        }

        return stored;
    }
}