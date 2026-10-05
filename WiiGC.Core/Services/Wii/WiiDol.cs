using System.Buffers.Binary;

namespace WiiGC.Core.Services.Wii;

internal static class WiiDol
{
    public const int HeaderSize = 0x100;

    private const int SectionCount = 18;
    private const int SizesOffset = 0x90;

    public static long GetSize(ReadOnlySpan<byte> header)
    {
        long end = HeaderSize;

        for (int i = 0; i < SectionCount; i++)
        {
            long offset = BinaryPrimitives.ReadUInt32BigEndian(header[(i * 4)..]);
            long size = BinaryPrimitives.ReadUInt32BigEndian(header[(SizesOffset + i * 4)..]);

            if (size != 0)
                end = Math.Max(end, offset + size);
        }

        return end;
    }
}