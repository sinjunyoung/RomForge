using System.Buffers.Binary;

namespace DolphinTool.Core.Services.Wii;

internal static class WiiPartitionTable
{
    private const long PartitionMagic = 0x10001;

    public static List<WiiPartitionSpec> Read(Rvz.IRvzInputSource input, long isoSize)
    {
        var offsets = new SortedSet<long>();
        var candidates = new List<(long Offset, uint Type)>();
        Span<byte> header = stackalloc byte[8];

        for (int group = 0; group < 4; group++)
        {
            long groupHeaderOffset = 0x40000 + group * 8;

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
                uint partitionType = BinaryPrimitives.ReadUInt32BigEndian(entry[4..]);

                if (partitionOffset <= 0 || partitionOffset >= isoSize || !offsets.Add(partitionOffset))
                    continue;

                candidates.Add((partitionOffset, partitionType));
            }
        }

        candidates.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        var result = new List<WiiPartitionSpec>();

        foreach (var (offset, _) in candidates)
        {
            var spec = TryRead(input, isoSize, offset);

            if (spec != null)
                result.Add(spec);
        }

        return result;
    }

    private static WiiPartitionSpec? TryRead(Rvz.IRvzInputSource input, long isoSize, long offset)
    {
        if (offset + 0x2C0 > isoSize)
            return null;

        Span<byte> magic = stackalloc byte[4];

        input.Read(offset, magic);

        if (BinaryPrimitives.ReadUInt32BigEndian(magic) != PartitionMagic)
            return null;

        Span<byte> pointers = stackalloc byte[8];

        input.Read(offset + 0x2B8, pointers);

        long dataOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(pointers) << 2;
        long dataSize = (long)BinaryPrimitives.ReadUInt32BigEndian(pointers[4..]) << 2;
        long dataStart = offset + dataOffset;
        long dataEnd = dataStart + dataSize;

        if (dataStart % Models.WiiLayout.BlockTotalSize != 0)
            return null;

        if (dataSize < Models.WiiLayout.BlockTotalSize)
            return null;

        if (dataEnd > isoSize)
            dataSize = isoSize - dataOffset - offset;

        if (dataSize < Models.WiiLayout.BlockTotalSize)
            return null;

        byte[] ticket = new byte[WiiTicket.Size];

        input.Read(offset, ticket);

        byte[] key = WiiTicket.DecryptTitleKey(ticket);
        long alignedSize = dataSize - dataSize % Models.WiiLayout.BlockTotalSize;

        return new WiiPartitionSpec(offset, dataStart, alignedSize, key);
    }
}