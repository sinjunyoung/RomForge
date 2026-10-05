using System.Buffers.Binary;
using System.Security.Cryptography;
using WiiGC.Core.Models;
using WiiGC.Core.Services;

namespace WiiGC.Core.Services.Wii;

internal static class WiiUsageScanner
{
    public const int MaxSectors = 143432 * 2;

    private const int SectorSize = 0x8000;
    private const int SectorDataSize = 0x7C00;
    private const int SectorDataSize4 = SectorDataSize >> 2;
    private const int HeadSectors = 0x50000 / SectorSize;
    private const int BootSize = 0x440 + 0x2000;
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
        MarkRaw(used, 0x40000, 0x20);

        Span<byte> group = stackalloc byte[8];

        for (int g = 0; g < 4; g++)
        {
            input.Read(0x40000 + g * 8, group);

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
        byte[] header = new byte[0x2C0];

        input.Read(spec.ContainerOffset, header);

        uint tmdSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x2A4));
        long tmdOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x2A8)) << 2;
        uint certSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x2AC));
        long certOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x2B0)) << 2;
        long h3Offset = (long)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x2B4)) << 2;

        MarkRaw(used, spec.ContainerOffset, 0x2C0);
        MarkRaw(used, spec.ContainerOffset + tmdOffset, tmdSize);

        if (certOffset != 0)
            MarkRaw(used, spec.ContainerOffset + certOffset, certSize);

        if (h3Offset != 0)
            MarkRaw(used, spec.ContainerOffset + h3Offset, 0x18000);

        var reader = new PartitionReader(input, used, spec);

        byte[] boot = new byte[BootSize];

        reader.Read(0, boot, true);

        uint dolOffset4 = BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x420));
        uint fstOffset4 = BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x424));
        uint fstSize4 = BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x428));

        byte[] dol = new byte[0x100];

        reader.Read((long)dolOffset4 << 2, dol, false);

        long dolSize = 0;

        for (int i = 0; i < 18; i++)
        {
            long end = (long)BinaryPrimitives.ReadUInt32BigEndian(dol.AsSpan(i * 4)) + BinaryPrimitives.ReadUInt32BigEndian(dol.AsSpan(0x90 + i * 4));

            if (dolSize < end)
                dolSize = end;
        }

        if (dolSize > ((long)fstOffset4 - dolOffset4) << 2)
            throw new InvalidDataException("MAIN.DOL 크기가 올바르지 않습니다.");

        reader.MarkPart(dolOffset4, dolSize);

        byte[] apploader = new byte[0x20];

        reader.Read(ApploaderOffset, apploader, false);

        long apploaderSize = 0x20L + BinaryPrimitives.ReadUInt32BigEndian(apploader.AsSpan(0x14)) + BinaryPrimitives.ReadUInt32BigEndian(apploader.AsSpan(0x18));

        reader.MarkPart(ApploaderOffset >> 2, apploaderSize);

        long userOffset = (ApploaderOffset + apploaderSize + 3) & ~3L;
        byte[] userHeader = new byte[0x10];

        if (reader.TryRead(userOffset, userHeader, false) && userHeader.AsSpan(0, 8).SequenceEqual("USER.BIN"u8))
            reader.MarkPart((userOffset + 0x10) >> 2, BinaryPrimitives.ReadUInt32BigEndian(userHeader.AsSpan(0x0C)));

        long fstSize = (long)fstSize4 << 2;

        if (fstSize < 12 || fstSize > 0x4000000)
            return;

        byte[] fst = new byte[fstSize];

        reader.Read((long)fstOffset4 << 2, fst, true);

        long entryCount = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(8));

        for (long i = 1; i < entryCount && (i + 1) * 12 <= fstSize; i++)
        {
            var entry = fst.AsSpan((int)(i * 12), 12);

            if (entry[0] != 0)
                continue;

            reader.MarkPart(BinaryPrimitives.ReadUInt32BigEndian(entry[4..]), BinaryPrimitives.ReadUInt32BigEndian(entry[8..]));
        }
    }

    private sealed class PartitionReader
    {
        private readonly IRvzInputSource _input;
        private readonly bool[] _used;
        private readonly WiiPartitionSpec _spec;
        private readonly long _delta;
        private readonly byte[] _encrypted = new byte[SectorSize];
        private readonly byte[] _plain = new byte[SectorDataSize];
        private readonly Aes _aes;
        private long _cached = -1;

        public PartitionReader(IRvzInputSource input, bool[] used, WiiPartitionSpec spec)
        {
            _input = input;
            _used = used;
            _spec = spec;
            _delta = spec.DataStart / SectorSize;
            _aes = Aes.Create();
            _aes.Key = spec.Key;
        }

        public bool TryRead(long offset, byte[] destination, bool mark)
        {
            try
            {
                Read(offset, destination, mark);

                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        public void Read(long offset, byte[] destination, bool mark)
        {
            int written = 0;

            while (written < destination.Length)
            {
                long cluster = offset / SectorDataSize;
                int inner = (int)(offset % SectorDataSize);
                int chunk = Math.Min(SectorDataSize - inner, destination.Length - written);

                LoadCluster(cluster);

                if (mark)
                {
                    long sector = _delta + cluster;

                    if (sector < MaxSectors)
                        _used[sector] = true;
                }

                Array.Copy(_plain, inner, destination, written, chunk);

                written += chunk;
                offset += chunk;
            }
        }

        public void MarkPart(long offset4, long size)
        {
            long first = offset4 / SectorDataSize4 + _delta;
            long end = (((size + SectorDataSize - 1) >> 2) + offset4) / SectorDataSize4 + _delta;

            if (end > MaxSectors)
                end = MaxSectors;

            for (long s = first; s < end; s++)
                _used[s] = true;
        }

        private void LoadCluster(long cluster)
        {
            if (_cached == cluster)
                return;

            long position = _spec.DataStart + cluster * SectorSize;

            if (position + SectorSize > _input.Length || cluster * SectorSize >= _spec.DataSize)
                throw new EndOfStreamException("파티션 범위를 벗어난 읽기입니다.");

            _input.Read(position, _encrypted);

            var iv = _encrypted.AsSpan(0x3D0, 16).ToArray();

            _aes.DecryptCbc(_encrypted.AsSpan(WiiLayout.BlockHeaderSize, SectorDataSize), iv, _plain, PaddingMode.None);

            _cached = cluster;
        }
    }
}