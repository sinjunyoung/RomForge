using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using WiiGC.Core.Models;
using ZstdSharp.Unsafe;

namespace WiiGC.Core.Services;

internal readonly record struct RvzRawEntry(long Offset, long Size, uint GroupIndex, uint GroupCount);

internal static class RvzHeaderWriter
{
    public const int DiscHeaderSize = 0x80;
    public const int Header1Size = 0x48;
    public const int Header2Size = 0xDC;
    public const int PartitionEntrySize = 0x30;

    private const uint RvzVersion = 0x01000000;
    private const uint RvzVersionWriteCompatible = 0x00030000;

    public static void ValidateCompressionLevel(int compressionLevel)
    {
        if (compressionLevel < ZstdSharp.Compressor.MinCompressionLevel || compressionLevel > ZstdSharp.Compressor.MaxCompressionLevel)
            throw new ArgumentOutOfRangeException(nameof(compressionLevel), "zstd 압축 레벨이 범위를 벗어났습니다.");
    }

    public static ZstdSharp.Compressor CreateCompressor(int compressionLevel)
    {
        var compressor = new ZstdSharp.Compressor(compressionLevel);

        compressor.SetParameter(ZSTD_cParameter.ZSTD_c_contentSizeFlag, 0);

        return compressor;
    }

    public static long EstimateUpperBound(long partitionTableBytes, long groupTableBytes) => Header1Size + Header2Size + partitionTableBytes + 24 + 0x100 + groupTableBytes * 9 / 16;

    public static long Align4(long value) => (value + 3) & ~3L;

    public static void Finish(SafeFileHandle output, uint discType, byte[] discHeader, long isoSize, int compressionLevel, int chunkSize, GroupEntry[] groups, IReadOnlyList<RvzRawEntry> rawEntries, byte[] partitionTable, long upperBound)
    {
        byte[] rawTable = new byte[rawEntries.Count * 24];

        for (int i = 0; i < rawEntries.Count; i++)
        {
            var entry = rawEntries[i];
            var span = rawTable.AsSpan(i * 24, 24);

            BinaryPrimitives.WriteUInt64BigEndian(span, (ulong)entry.Offset);
            BinaryPrimitives.WriteUInt64BigEndian(span[8..], (ulong)entry.Size);
            BinaryPrimitives.WriteUInt32BigEndian(span[16..], entry.GroupIndex);
            BinaryPrimitives.WriteUInt32BigEndian(span[20..], entry.GroupCount);
        }

        byte[] groupTable = new byte[groups.Length * 12];

        for (int i = 0; i < groups.Length; i++)
        {
            var span = groupTable.AsSpan(i * 12, 12);

            BinaryPrimitives.WriteUInt32BigEndian(span, groups[i].DataOffset4);
            BinaryPrimitives.WriteUInt32BigEndian(span[4..], groups[i].DataSizeField);
            BinaryPrimitives.WriteUInt32BigEndian(span[8..], groups[i].RvzPackedSize);
        }

        using var compressor = CreateCompressor(compressionLevel);
        byte[] compressedRaw = CompressTable(compressor, rawTable);
        byte[] compressedGroups = CompressTable(compressor, groupTable);
        long cursor = Header1Size + Header2Size;
        long partitionOffset = WriteTable(output, partitionTable, ref cursor, upperBound);
        long rawOffset = WriteTable(output, compressedRaw, ref cursor, upperBound);
        long groupOffset = WriteTable(output, compressedGroups, ref cursor, upperBound);
        byte[] header2 = new byte[Header2Size];

        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(0), discType);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(4), (uint)RvzCompressionType.Zstd);
        BinaryPrimitives.WriteInt32BigEndian(header2.AsSpan(8), compressionLevel);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(12), (uint)chunkSize);
        discHeader.CopyTo(header2.AsSpan(16));
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(144), (uint)(partitionTable.Length / PartitionEntrySize));
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(148), PartitionEntrySize);
        BinaryPrimitives.WriteUInt64BigEndian(header2.AsSpan(152), (ulong)partitionOffset);
        SHA1.HashData(partitionTable, header2.AsSpan(160, 20));
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(180), (uint)rawEntries.Count);
        BinaryPrimitives.WriteUInt64BigEndian(header2.AsSpan(184), (ulong)rawOffset);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(192), (uint)compressedRaw.Length);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(196), (uint)groups.Length);
        BinaryPrimitives.WriteUInt64BigEndian(header2.AsSpan(200), (ulong)groupOffset);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(208), (uint)compressedGroups.Length);

        header2[212] = 0;

        byte[] header1 = new byte[Header1Size];

        header1[0] = (byte)'R';
        header1[1] = (byte)'V';
        header1[2] = (byte)'Z';
        header1[3] = 1;

        BinaryPrimitives.WriteUInt32BigEndian(header1.AsSpan(4), RvzVersion);
        BinaryPrimitives.WriteUInt32BigEndian(header1.AsSpan(8), RvzVersionWriteCompatible);
        BinaryPrimitives.WriteUInt32BigEndian(header1.AsSpan(12), Header2Size);
        SHA1.HashData(header2, header1.AsSpan(16, 20));
        BinaryPrimitives.WriteUInt64BigEndian(header1.AsSpan(36), (ulong)isoSize);
        BinaryPrimitives.WriteUInt64BigEndian(header1.AsSpan(44), (ulong)RandomAccess.GetLength(output));
        SHA1.HashData(header1.AsSpan(0, Header1Size - 20), header1.AsSpan(Header1Size - 20, 20));
        RandomAccess.Write(output, header1, 0);
        RandomAccess.Write(output, header2, Header1Size);
    }

    private static byte[] CompressTable(ZstdSharp.Compressor compressor, byte[] table)
    {
        byte[] buffer = new byte[ZstdSharp.Compressor.GetCompressBound(table.Length)];
        int written = compressor.Wrap(table, buffer);

        return buffer.AsSpan(0, written).ToArray();
    }

    private static long WriteTable(SafeFileHandle output, byte[] data, ref long cursor, long upperBound)
    {
        if (cursor <= upperBound && cursor + data.Length > upperBound)
            cursor = Align4(RandomAccess.GetLength(output));

        long offset = cursor;

        RandomAccess.Write(output, data, cursor);

        cursor = Align4(cursor + data.Length);

        return offset;
    }
}