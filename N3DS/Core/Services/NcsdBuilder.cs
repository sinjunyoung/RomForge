using N3DS.Core.Interfaces;
using N3DS.Core.Models;
using System.Buffers.Binary;

namespace N3DS.Core.Services;

public class NcsdBuilder
{
    private const int MediaUnit = 0x200;
    private const int NcsdHeadSize = 0x4000;

    public static async Task BuildAsync(INcsdSource ctx, Stream output, Action<long, long>? progress = null, CancellationToken ct = default)
    {
        var partitions = new List<(int index, NcchHeader header, long size)>();
        byte[] hdrBuf = new byte[0x200];

        foreach (var chunk in ctx.Contents.OrderBy(c => c.ContentIndex))
        {
            if (chunk.ContentIndex > 7)
                continue;

            var ncchHeader = await ctx.GetNcchHeaderAsync(chunk.ContentIndex, ct);

            partitions.Add((chunk.ContentIndex, ncchHeader, chunk.ContentSize));
        }

        try
        {
            var partitionMap = new (uint offset, uint size)[8];
            uint currentOffset = NcsdHeadSize / MediaUnit;

            foreach (var (index, _, size) in partitions)
            {
                uint sizeInUnits = (uint)AlignUp(size, MediaUnit) / MediaUnit;

                partitionMap[index] = (currentOffset, sizeInUnits);
                currentOffset += sizeInUnits;
            }

            uint totalSizeInUnits = currentOffset;
            byte[] ncsdHeader = BuildNcsdHeader(partitions, partitionMap, totalSizeInUnits);

            await output.WriteAsync(ncsdHeader, ct);

            byte[] cardInfo = BuildCardInfoHeader(partitions, partitionMap);

            await output.WriteAsync(cardInfo, ct);

            long totalBytes = partitions.Sum(p => p.size);
            long globalWritten = 0;

            foreach (var (index, _, size) in partitions)
            {
                ct.ThrowIfCancellationRequested();

                long aligned = AlignUp(size, MediaUnit);
                Action<long, long>? partitionProgress = progress != null ? (pWritten, _) => progress(globalWritten + pWritten, totalBytes) : null;

                await ctx.WriteContentAsync(index, output, 0, partitionProgress, ct);

                long padding = aligned - size;

                if (padding > 0)
                {
                    byte[] pad = new byte[padding];

                    await output.WriteAsync(pad, ct);
                }

                globalWritten += size;
            }
        }
        finally
        {
        }
    }

    public static async Task<long> CalculateSizeAsync(NcchUnpackResult unpack, byte[] exefsBlock, Stream romfsStream, CancellationToken ct = default)
    {
        long ncchSize = MediaUnit;

        if (unpack.ExHeader != null)
            ncchSize += unpack.ExHeader.Length;

        if (unpack.Logo != null)
            ncchSize = AlignUp(ncchSize, MediaUnit) + AlignUp(unpack.Logo.Length, MediaUnit);

        if (unpack.PlainRegion != null)
            ncchSize = AlignUp(ncchSize, MediaUnit) + AlignUp(unpack.PlainRegion.Length, MediaUnit);

        if (exefsBlock.Length > 0)
            ncchSize = AlignUp(ncchSize, MediaUnit) + AlignUp(exefsBlock.Length, MediaUnit);

        if (romfsStream.Length > 0)
        {
            byte[] ivfcBuf = new byte[IvfcHeader.Size];

            romfsStream.Position = 0;

            await romfsStream.ReadExactlyAsync(ivfcBuf, ct);

            ncchSize = AlignUp(ncchSize, NcchBuilder.RomFsAlign) + AlignUp(romfsStream.Length, MediaUnit);
        }

        return AlignUp(ncchSize, MediaUnit);
    }

    public static long CalculateOutputSize(INcsdSource ctx)
    {
        return NcsdHeadSize + ctx.Contents
            .Where(c => c.ContentIndex <= 7)
            .Sum(c => AlignUp(c.ContentSize, MediaUnit));
    }

    private static byte[] BuildNcsdHeader(List<(int index, NcchHeader header, long size)> partitions, (uint offset, uint size)[] partitionMap, uint totalSizeInUnits)
    {
        byte[] buf = new byte[0x200];

        buf[0x100] = (byte)'N';
        buf[0x101] = (byte)'C';
        buf[0x102] = (byte)'S';
        buf[0x103] = (byte)'D';

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x104), totalSizeInUnits);

        var part0 = partitions.FirstOrDefault(p => p.index == 0);

        if (part0 != default)
            BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x108), part0.header.ProgramId);

        for (int i = 0; i < 8; i++)
        {
            int tableOff = 0x120 + i * 8;

            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(tableOff), partitionMap[i].offset);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(tableOff + 4), partitionMap[i].size);
        }

        if (part0 != default)
            part0.header.ExtendedHeaderHash.CopyTo(buf, 0x160);

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x180), 0x312);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x184), 0x00);

        buf[0x188 + 4] = 0x01;
        buf[0x188 + 5] = 0x01;

        foreach (var (index, header, _) in partitions)
        {
            int idOff = 0x190 + index * 8;

            BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(idOff), header.PartitionId);
        }

        return buf;
    }

    private static byte[] BuildCardInfoHeader(List<(int index, NcchHeader header, long size)> partitions, (uint offset, uint size)[] partitionMap)
    {
        byte[] buf = new byte[0x3E00];

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x00), 0xFFFFFFFF);

        uint filledSize = 0;

        foreach (var (index, _, _) in partitions)
            filledSize = Math.Max(filledSize, partitionMap[index].offset + partitionMap[index].size);

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x100), filledSize);
        
        return buf;
    }

    private static long AlignUp(long value, long alignment) => (value + alignment - 1) & ~(alignment - 1);
}