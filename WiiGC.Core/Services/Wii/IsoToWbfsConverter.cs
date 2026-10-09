using System.Buffers.Binary;

namespace WiiGC.Core.Services.Wii;

public static class IsoToWbfsConverter
{
    private const int HdSectorShift = 9;
    private const int WbfsSectorShift = 21;
    private const int HdSectorSize = 1 << HdSectorShift;
    private const int WbfsSectorSize = 1 << WbfsSectorShift;
    private const int WiiSectorSize = 0x8000;
    private const int SectorsPerBlock = WbfsSectorSize / WiiSectorSize;
    private const int DiscHeaderSize = 256;
    private const long WiiSectorCount = 143432 * 2;
    private const int BlocksPerDisc = (int)(WiiSectorCount >> (WbfsSectorShift - 15));
    private const int FreeBlockTableOffset = WbfsSectorSize - 1024;
    private const int FreeBlockTableWords = 128;
    private const int FreeBlockTableBlocks = 8191;

    public static void Convert(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);

        Convert(input, outputPath, progress, ct);
    }

    internal static void Convert(IRvzInputSource input, string outputPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        bool succeeded = false;

        try
        {
            Span<byte> header = stackalloc byte[0x20];

            if (input is WbfsSource || input.Length < DiscHeaderSize)
                throw new InvalidDataException("Wii ISO 파일이 아닙니다.");

            input.Read(0, header);

            if (!RvzWiiWriter.IsWii(header))
                throw new InvalidDataException("Wii ISO 파일이 아닙니다. WBFS는 Wii 디스크만 지원합니다.");

            long length = input.Length;
            long blockCount = (length + WbfsSectorSize - 1) / WbfsSectorSize;

            if (blockCount > BlocksPerDisc)
                throw new InvalidDataException("Wii 디스크 최대 크기를 초과했습니다.");

            bool[]? usage = WiiUsageScanner.Scan(input);

            using var output = File.OpenHandle(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None);

            var wlbaTable = new ushort[BlocksPerDisc];
            byte[] buffer = new byte[WbfsSectorSize];
            byte[] discHeader = new byte[DiscHeaderSize];
            int usedBlocks = 0;

            input.Read(0, discHeader);

            for (long block = 0; block < blockCount; block++)
            {
                ct.ThrowIfCancellationRequested();

                bool used = usage is null
                    ? ReadBlock(input, block, length, buffer)
                    : ReadUsedSectors(input, usage, block, length, buffer);

                if (used)
                {
                    usedBlocks++;
                    wlbaTable[block] = checked((ushort)usedBlocks);

                    RandomAccess.Write(output, buffer, (long)usedBlocks * WbfsSectorSize);
                }

                progress?.Invoke(Math.Min(1.0, (double)(block + 1) / blockCount) * 0.99);
            }

            if (usedBlocks == 0)
                throw new InvalidDataException("변환할 데이터가 없습니다.");

            long totalSize = (long)(usedBlocks + 1) * WbfsSectorSize;

            RandomAccess.SetLength(output, totalSize);

            byte[] head = new byte[HdSectorSize];

            head[0] = (byte)'W';
            head[1] = (byte)'B';
            head[2] = (byte)'F';
            head[3] = (byte)'S';

            BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(4), (uint)(totalSize >> HdSectorShift));

            head[8] = HdSectorShift;
            head[9] = WbfsSectorShift;
            head[10] = 1;
            head[12] = 1;

            RandomAccess.Write(output, head, 0);

            int discInfoSize = (DiscHeaderSize + BlocksPerDisc * 2 + HdSectorSize - 1) / HdSectorSize * HdSectorSize;
            byte[] discInfo = new byte[discInfoSize];

            discHeader.CopyTo(discInfo, 0);

            for (int i = 0; i < BlocksPerDisc; i++)
                BinaryPrimitives.WriteUInt16BigEndian(discInfo.AsSpan(DiscHeaderSize + i * 2), wlbaTable[i]);

            RandomAccess.Write(output, discInfo, HdSectorSize);
            RandomAccess.Write(output, BuildFreeBlockTable(usedBlocks), FreeBlockTableOffset);

            progress?.Invoke(1.0);

            succeeded = true;
        }
        finally
        {
            OutputGuard.DeleteIfFailed(outputPath, succeeded);
        }
    }

    private static bool ReadBlock(IRvzInputSource input, long block, long length, byte[] buffer)
    {
        long offset = block * WbfsSectorSize;
        int size = (int)Math.Min(WbfsSectorSize, length - offset);

        input.Read(offset, buffer.AsSpan(0, size));

        if (size < WbfsSectorSize)
            buffer.AsSpan(size).Clear();

        return buffer.AsSpan().ContainsAnyExcept((byte)0);
    }

    private static bool ReadUsedSectors(IRvzInputSource input, bool[] usage, long block, long length, byte[] buffer)
    {
        long firstSector = block * SectorsPerBlock;
        bool any = false;

        for (int i = 0; i < SectorsPerBlock; i++)
        {
            long sector = firstSector + i;

            if (sector < usage.Length && usage[sector])
            {
                any = true;
                break;
            }
        }

        if (!any)
            return false;

        buffer.AsSpan().Clear();

        for (int i = 0; i < SectorsPerBlock; i++)
        {
            long sector = firstSector + i;

            if (sector >= usage.Length || !usage[sector])
                continue;

            long offset = sector * WiiSectorSize;

            if (offset >= length)
                continue;

            int size = (int)Math.Min(WiiSectorSize, length - offset);

            input.Read(offset, buffer.AsSpan(i * WiiSectorSize, size));
        }

        return true;
    }

    private static byte[] BuildFreeBlockTable(int usedBlocks)
    {
        byte[] table = new byte[FreeBlockTableWords * 4];

        for (int word = 0; word < FreeBlockTableWords; word++)
        {
            uint mask = 0;

            for (int bit = 0; bit < 32; bit++)
            {
                int index = word * 32 + bit;

                if (index >= usedBlocks && index < FreeBlockTableBlocks)
                    mask |= 1u << bit;
            }

            BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(word * 4), mask);
        }

        return table;
    }
}