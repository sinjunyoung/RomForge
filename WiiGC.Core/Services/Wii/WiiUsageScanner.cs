using System.Buffers.Binary;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal static class WiiUsageScanner
{
    public const int MaxSectors = WiiLayout.DiscSectorCount;

    private const int SectorSize = 0x8000;
    private const int SectorDataSize = 0x7C00;
    private const int SectorDataSize4 = SectorDataSize >> 2;
    private const int HeadSectors = 0x50000 / SectorSize;
    private const int BootSize = WiiFolderLayout.ApploaderOffset;
    private const int ApploaderOffset = 0x2440;

    public static bool[]? Scan(IRvzInputSource input)
    {
        try
        {
            var used = new bool[MaxSectors];

            ScanHead(input, used);

            var specs = WiiPartitionTable.Read(input, input.Length);

            if (specs.Count == 0)
                return null;

            foreach (var spec in specs)
                ScanPartition(input, used, spec);

            return used;
        }
        catch
        {
            return null;
        }
    }

    private static void MarkRaw(bool[] used, long start, long size)
    {
        if (size <= 0)
            return;

        long first = start / SectorSize;
        long last = (start + size - 1) / SectorSize;

        for (long s = first; s <= last && s < MaxSectors; s++)
            used[s] = true;
    }

    private static void ScanHead(IRvzInputSource input, bool[] used)
    {
        MarkRaw(used, 0, BootSize);
        MarkRaw(used, WiiLayout.PartitionTableOffset, WiiLayout.PartitionTableSize);

        Span<byte> group = stackalloc byte[8];

        for (int g = 0; g < 4; g++)
        {
            input.Read(WiiLayout.PartitionTableOffset + g * 8, group);

            uint count = BinaryPrimitives.ReadUInt32BigEndian(group);
            long tableOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(group[4..]) << 2;

            if (count > 0 && tableOffset > 0)
                MarkRaw(used, tableOffset, count * 8L);
        }

        MarkRaw(used, 0x4E000, 0x20);
        MarkRaw(used, 0x4FFFC, 4);

        byte[] sector = new byte[SectorSize];

        for (int s = 0; s < HeadSectors; s++)
        {
            if (used[s])
                continue;

            long offset = (long)s * SectorSize;

            if (offset + SectorSize > input.Length)
                break;

            input.Read(offset, sector);

            if (sector.AsSpan().ContainsAnyExcept((byte)0))
                used[s] = true;
        }
    }

    private static void ScanPartition(IRvzInputSource input, bool[] used, WiiPartitionSpec spec)
    {
        byte[] header = new byte[WiiFolderLayout.PartitionHeaderSize];

        input.Read(spec.ContainerOffset, header);

        uint tmdSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(WiiPartitionHeader.TmdSizeField));
        long tmdOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(WiiPartitionHeader.TmdOffsetField)) << 2;
        uint certSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(WiiPartitionHeader.CertSizeField));
        long certOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(WiiPartitionHeader.CertOffsetField)) << 2;
        long h3Offset = (long)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(WiiPartitionHeader.H3OffsetField)) << 2;

        MarkRaw(used, spec.ContainerOffset, 0x2C0);
        MarkRaw(used, spec.ContainerOffset + tmdOffset, tmdSize);

        if (certOffset != 0)
            MarkRaw(used, spec.ContainerOffset + certOffset, certSize);

        if (h3Offset != 0)
            MarkRaw(used, spec.ContainerOffset + h3Offset, 0x18000);

        using var reader = new WiiPartitionReader(input, spec);
        byte[] boot = new byte[BootSize];

        reader.Read(0, boot, used);

        uint dolOffset4 = BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x420));
        uint fstOffset4 = BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x424));
        uint fstSize4 = BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x428));

        byte[] dol = new byte[0x100];

        reader.Read((long)dolOffset4 << 2, dol);

        long dolSize = 0;

        for (int i = 0; i < 18; i++)
        {
            long end = (long)BinaryPrimitives.ReadUInt32BigEndian(dol.AsSpan(i * 4)) + BinaryPrimitives.ReadUInt32BigEndian(dol.AsSpan(0x90 + i * 4));

            if (dolSize < end)
                dolSize = end;
        }

        if (dolSize > ((long)fstOffset4 - dolOffset4) << 2)
            throw new InvalidDataException("MAIN.DOL 크기가 올바르지 않습니다.");

        MarkPart(used, spec, dolOffset4, dolSize);

        byte[] apploader = new byte[0x20];

        reader.Read(ApploaderOffset, apploader);

        long apploaderSize = 0x20L + BinaryPrimitives.ReadUInt32BigEndian(apploader.AsSpan(0x14)) + BinaryPrimitives.ReadUInt32BigEndian(apploader.AsSpan(0x18));

        MarkPart(used, spec, ApploaderOffset >> 2, apploaderSize);

        long userOffset = (ApploaderOffset + apploaderSize + 3) & ~3L;
        byte[] userHeader = new byte[0x10];

        if (TryRead(reader, userOffset, userHeader) && userHeader.AsSpan(0, 8).SequenceEqual("USER.BIN"u8))
            MarkPart(used, spec, (userOffset + 0x10) >> 2, BinaryPrimitives.ReadUInt32BigEndian(userHeader.AsSpan(0x0C)));

        long fstSize = (long)fstSize4 << 2;

        if (fstSize < 12 || fstSize > 0x4000000)
            return;

        byte[] fst = new byte[fstSize];

        reader.Read((long)fstOffset4 << 2, fst, used);

        long entryCount = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(8));

        for (long i = 1; i < entryCount && (i + 1) * 12 <= fstSize; i++)
        {
            var entry = fst.AsSpan((int)(i * 12), 12);

            if (entry[0] != 0)
                continue;

            MarkPart(used, spec, BinaryPrimitives.ReadUInt32BigEndian(entry[4..]), BinaryPrimitives.ReadUInt32BigEndian(entry[8..]));
        }
    }

    private static bool TryRead(WiiPartitionReader reader, long offset, byte[] destination)
    {
        try
        {
            reader.Read(offset, destination);

            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    private static void MarkPart(bool[] used, WiiPartitionSpec spec, long offset4, long size)
    {
        long delta = spec.DataStart / SectorSize;
        long first = offset4 / SectorDataSize4 + delta;
        long end = (((size + SectorDataSize - 1) >> 2) + offset4) / SectorDataSize4 + delta;

        if (end > MaxSectors)
            end = MaxSectors;

        for (long s = first; s < end; s++)
            used[s] = true;
    }
}