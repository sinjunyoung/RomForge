using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public struct DiffHeader
{
    public const int Size = 0x5C;

    public byte[] Magic;

    public uint Version;

    public ulong SecondaryTableOffset;

    public ulong PrimaryTableOffset;

    public ulong TableSize;

    public ulong PartitionOffset;

    public ulong PartitionSize;

    public byte ActiveTable;

    public byte[] Padding;

    public byte[] Sha;

    public ulong UniqueId;

    public static DiffHeader Read(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        f.Read(pos, buf, 0, Size);

        return new DiffHeader
        {
            Magic = buf[0x00..0x04],
            Version = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x04)),
            SecondaryTableOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x08)),
            PrimaryTableOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x10)),
            TableSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x18)),
            PartitionOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x20)),
            PartitionSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x28)),
            ActiveTable = buf[0x30],
            Padding = buf[0x31..0x34],
            Sha = buf[0x34..0x54],
            UniqueId = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x54)),
        };
    }

    public readonly void Write(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        Magic.CopyTo(buf, 0x00);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x04), Version);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x08), SecondaryTableOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x10), PrimaryTableOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x18), TableSize);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x20), PartitionOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x28), PartitionSize);

        buf[0x30] = ActiveTable;

        Padding.CopyTo(buf, 0x31);
        Sha.CopyTo(buf, 0x34);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x54), UniqueId);
        f.Write(pos, buf, 0, Size);
    }
}