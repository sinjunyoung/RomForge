using System.Buffers.Binary;

namespace N3DS.Core.Models;

public class IvfcHeader
{
    public const int Size = 0x5C;

    public const int HeaderAlign = 0x10;

    public uint Magic { get; init; }

    public uint TypeId { get; init; }

    public uint MasterHashSize { get; init; }

    public IvfcLevelEntry[] Levels { get; init; } = new IvfcLevelEntry[3];

    public uint HeaderSize { get; init; }

    public static IvfcHeader Parse(byte[] data, int offset = 0)
    {
        uint masterHashSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x08));
        var levels = new IvfcLevelEntry[3];

        for (int i = 0; i < 3; i++)
        {
            int b = offset + 0x0C + i * 0x18;

            levels[i] = new IvfcLevelEntry
            {
                Offset = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(b + 0x00)),
                Size = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(b + 0x08)),
                BlockSizeLog2 = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(b + 0x10)),
            };
        }

        return new IvfcHeader
        {
            Magic = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x00)),
            TypeId = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x04)),
            MasterHashSize = masterHashSize,
            Levels = levels,
            HeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x54)),
        };
    }

    public long GetDataLevel2Offset()
    {
        long masterHashOffset = AlignUp(Size, HeaderAlign);
        long dataBlockSize = 1L << (int)Levels[2].BlockSizeLog2;

        return AlignUp(masterHashOffset + MasterHashSize, dataBlockSize);
    }

    private static long AlignUp(long v, long a) => (v + a - 1) & ~(a - 1);
}