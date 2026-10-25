using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public struct OffsetOrFatFile
{
    public const int Size = 8;

    public uint BlockIndex;

    public uint BlockCount;

    public static OffsetOrFatFile FromOffset(ulong offset) => new()
    {
        BlockIndex = (uint)(offset & 0xFFFFFFFF),
        BlockCount = (uint)(offset >> 32),
    };

    public readonly ulong ToOffset() => BlockIndex | ((ulong)BlockCount << 32);

    public static OffsetOrFatFile Read(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        f.Read(pos, buf, 0, Size);

        return new OffsetOrFatFile
        {
            BlockIndex = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0)),
            BlockCount = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(4)),
        };
    }

    public readonly void Write(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0), BlockIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4), BlockCount);
        f.Write(pos, buf, 0, Size);
    }
}