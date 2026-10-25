using System.Buffers.Binary;
using WiiGC.Core.Interfaces;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal static class PartitionTable
{
    private const long PartitionMagic = 0x10001;

    public static List<PartitionSpec> Read(IRvzInputSource input, long isoSize)
    {
        var offsets = new SortedSet<long>();
        Span<byte> header = stackalloc byte[8];

        for (int group = 0; group < 4; group++)
        {
            long groupHeaderOffset = Layout.PartitionTableOffset + group * 8;

            if (groupHeaderOffset + 8 > isoSize)
                break;

            input.Read(groupHeaderOffset, header);

            uint count = BinaryPrimitives.ReadUInt32BigEndian(header);
            long tableOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(header[4..]) << 2;

            if (count == 0 || tableOffset <= 0 || tableOffset + (long)count * 8 > isoSize)
                continue;

            byte[] table = new byte[count * 8];

            input.Read(tableOffset, table);

            for (int i = 0; i < count; i++)
            {
                var entry = table.AsSpan(i * 8, 8);
                long partitionOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(entry) << 2;

                if (partitionOffset > 0 && partitionOffset < isoSize)
                    offsets.Add(partitionOffset);
            }
        }

        var result = new List<PartitionSpec>();

        foreach (long offset in offsets)
        {
            var spec = TryRead(input, isoSize, offset);

            if (spec != null)
                result.Add(spec);
        }

        return result;
    }

    private static PartitionSpec? TryRead(IRvzInputSource input, long isoSize, long offset)
    {
        if (offset + 0x2C0 > isoSize)
            return null;

        Span<byte> magic = stackalloc byte[4];

        input.Read(offset, magic);

        if (BinaryPrimitives.ReadUInt32BigEndian(magic) != PartitionMagic)
            return null;

        Span<byte> pointers = stackalloc byte[8];

        input.Read(offset + PartitionHeader.DataOffsetField, pointers);

        long dataOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(pointers) << 2;
        long dataSize = (long)BinaryPrimitives.ReadUInt32BigEndian(pointers[4..]) << 2;
        long dataStart = offset + dataOffset;
        long dataEnd = dataStart + dataSize;

        if (dataStart % Layout.BlockTotalSize != 0)
            return null;

        if (dataSize < Layout.BlockTotalSize)
            return null;

        if (dataEnd > isoSize)
            dataSize = isoSize - dataOffset - offset;

        if (dataSize < Layout.BlockTotalSize)
            return null;

        byte[] ticket = new byte[Ticket.Size];

        input.Read(offset, ticket);

        byte[] key = Ticket.DecryptTitleKey(ticket);
        long alignedSize = dataSize - dataSize % Layout.BlockTotalSize;

        return new PartitionSpec(offset, dataStart, alignedSize, key);
    }
}