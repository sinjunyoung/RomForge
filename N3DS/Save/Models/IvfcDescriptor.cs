using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public struct IvfcDescriptor
{
    public const int Size = 0x78;

    public byte[] Magic;

    public uint Version;

    public ulong MasterHashSize;

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

    public ulong Level4Offset;

    public ulong Level4Size;

    public uint Level4BlockLog;

    public uint Padding4;

    public ulong IvfcDescriptorSize;

    public static IvfcDescriptor Read(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        f.Read(pos, buf, 0, Size);

        return new IvfcDescriptor
        {
            Magic = buf[0..4],
            Version = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x04)),
            MasterHashSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x08)),
            Level1Offset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x10)),
            Level1Size = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x18)),
            Level1BlockLog = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x20)),
            Padding1 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x24)),
            Level2Offset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x28)),
            Level2Size = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x30)),
            Level2BlockLog = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x38)),
            Padding2 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x3C)),
            Level3Offset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x40)),
            Level3Size = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x48)),
            Level3BlockLog = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x50)),
            Padding3 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x54)),
            Level4Offset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x58)),
            Level4Size = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x60)),
            Level4BlockLog = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x68)),
            Padding4 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x6C)),
            IvfcDescriptorSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x70)),
        };
    }

    public readonly void Write(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        Magic.CopyTo(buf, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x04), Version);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x08), MasterHashSize);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x10), Level1Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x18), Level1Size);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x20), Level1BlockLog);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x24), Padding1);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x28), Level2Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x30), Level2Size);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x38), Level2BlockLog);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x3C), Padding2);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x40), Level3Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x48), Level3Size);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x50), Level3BlockLog);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x54), Padding3);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x58), Level4Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x60), Level4Size);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x68), Level4BlockLog);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x6C), Padding4);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x70), IvfcDescriptorSize);
        f.Write(pos, buf, 0, Size);
    }
}