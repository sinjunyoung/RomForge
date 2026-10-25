using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public struct DifiHeader
{
    public const int Size = 0x44;

    public byte[] Magic;

    public uint Version;

    public ulong IvfcDescriptorOffset;

    public ulong IvfcDescriptorSize;

    public ulong DpfsDescriptorOffset;

    public ulong DpfsDescriptorSize;

    public ulong PartitionHashOffset;

    public ulong PartitionHashSize;

    public byte ExternalIvfcLevel4;

    public byte DpfsSelector;

    public ushort Padding;

    public ulong IvfcLevel4Offset;

    public static DifiHeader Read(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        f.Read(pos, buf, 0, Size);

        return new DifiHeader
        {
            Magic = buf[0..4],
            Version = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x04)),
            IvfcDescriptorOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x08)),
            IvfcDescriptorSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x10)),
            DpfsDescriptorOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x18)),
            DpfsDescriptorSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x20)),
            PartitionHashOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x28)),
            PartitionHashSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x30)),
            ExternalIvfcLevel4 = buf[0x38],
            DpfsSelector = buf[0x39],
            Padding = BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(0x3A)),
            IvfcLevel4Offset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x3C)),
        };
    }

    public readonly void Write(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        Magic.CopyTo(buf, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x04), Version);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x08), IvfcDescriptorOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x10), IvfcDescriptorSize);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x18), DpfsDescriptorOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x20), DpfsDescriptorSize);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x28), PartitionHashOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x30), PartitionHashSize);

        buf[0x38] = ExternalIvfcLevel4;
        buf[0x39] = DpfsSelector;

        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x3A), Padding);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x3C), IvfcLevel4Offset);
        f.Write(pos, buf, 0, Size);
    }
}