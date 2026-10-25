using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public struct DpfsDescriptor
{
    public const int Size = 0x50;

    public byte[] Magic;

    public uint Version;

    public ulong Level1Offset;

    public ulong Level1Size;

    public uint Level1BlockLog;

    public uint Padding1;

    public ulong Level2Offset;

    public ulong Level2Size;

    public uint Level2BlockLog;

    public uint Padding2;

    public ulong Level3Offset;

    public ulong Level3Size;

    public uint Level3BlockLog;

    public uint Padding3;

    public static DpfsDescriptor Read(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        f.Read(pos, buf, 0, Size);

        return new DpfsDescriptor
        {
            Magic = buf[0..4],
            Version = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x04)),
            Level1Offset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x08)),
            Level1Size = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x10)),
            Level1BlockLog = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x18)),
            Padding1 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x1C)),
            Level2Offset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x20)),
            Level2Size = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x28)),
            Level2BlockLog = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x30)),
            Padding2 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x34)),
            Level3Offset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x38)),
            Level3Size = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x40)),
            Level3BlockLog = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x48)),
            Padding3 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x4C)),
        };
    }

    public readonly void Write(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        Magic.CopyTo(buf, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x04), Version);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x08), Level1Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x10), Level1Size);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x18), Level1BlockLog);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x1C), Padding1);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x20), Level2Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x28), Level2Size);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x30), Level2BlockLog);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x34), Padding2);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x38), Level3Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x40), Level3Size);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x48), Level3BlockLog);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x4C), Padding3);
        f.Write(pos, buf, 0, Size);
    }
}