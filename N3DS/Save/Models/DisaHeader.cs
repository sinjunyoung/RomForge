using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public struct DisaHeader
{
    public const int Size = 0x69;

    public byte[] Magic;

    public uint Version;

    public uint PartitionCount;

    public uint Padding1;

    public ulong SecondaryTableOffset;

    public ulong PrimaryTableOffset;

    public ulong TableSize;

    public (ulong Offset, ulong Size)[] PartitionDescriptor;

    public (ulong Offset, ulong Size)[] Partition;

    public byte ActiveTable;

    public static DisaHeader Read(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        f.Read(pos, buf, 0, Size);

        return new DisaHeader
        {
            Magic = buf[0x00..0x04],
            Version = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x04)),
            PartitionCount = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x08)),
            Padding1 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x0C)),
            SecondaryTableOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x10)),
            PrimaryTableOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x18)),
            TableSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x20)),
            PartitionDescriptor =
            [
                (BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x28)),
                 BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x30))),
                (BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x38)),
                 BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x40))),
            ],
            Partition =
            [
                (BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x48)),
                 BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x50))),
                (BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x58)),
                 BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x60))),
            ],
            ActiveTable = buf[0x68],
        };
    }

    public readonly void Write(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        Magic.CopyTo(buf, 0x00);

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x04), Version);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x08), PartitionCount);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x0C), Padding1);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x10), SecondaryTableOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x18), PrimaryTableOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x20), TableSize);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x28), PartitionDescriptor[0].Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x30), PartitionDescriptor[0].Size);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x38), PartitionDescriptor[1].Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x40), PartitionDescriptor[1].Size);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x48), Partition[0].Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x50), Partition[0].Size);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x58), Partition[1].Offset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x60), Partition[1].Size);

        buf[0x68] = ActiveTable;

        f.Write(pos, buf, 0, Size);
    }
}