using DolphinTool.Core.Models;
using Microsoft.Win32.SafeHandles;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DolphinTool.Core.Rvz;

internal sealed class RvzFile
{
    private const uint RvzVersion = 0x01000000;
    private const uint RvzVersionReadCompatible = 0x00030000;
    private const uint WiaVersion = 0x01000000;
    private const uint WiaVersionReadCompatible = 0x00080000;
    private const int Header1Size = 0x48;
    private const int Header2FullSize = 0xDC;
    private const int Header2MinSize = Header2FullSize - 7;
    private const int PartitionEntrySize = 0x30;
    private const int RawDataEntrySize = 0x18;
    private const int RvzGroupEntrySize = 0x0C;
    private const int WiaGroupEntrySize = 0x08;

    public required RvzCompressionType Compression { get; init; }

    public required uint DiscType { get; init; }

    public required uint ChunkSize { get; init; }

    public required long IsoSize { get; init; }

    public required long FileLength { get; init; }

    public required byte[] DiscHeader { get; init; }

    public required byte[] CompressorData { get; init; }

    public required PartitionEntry[] Partitions { get; init; }

    public required RawDataEntry[] RawEntries { get; init; }

    public required GroupEntry[] Groups { get; init; }

    public static RvzFile Open(SafeFileHandle handle)
    {
        long fileLength = RandomAccess.GetLength(handle);

        if (fileLength < Header1Size + Header2MinSize)
            throw new InvalidDataException("RVZ 파일이 너무 작습니다.");

        byte[] h1 = new byte[Header1Size];

        RvzIo.ReadExactly(handle, h1, 0);

        if (!RvzMagic.IsRvz(h1) && !RvzMagic.IsWia(h1))
            throw new InvalidDataException("RVZ/WIA 파일이 아닙니다.");

        bool isRvz = RvzMagic.IsRvz(h1);
        int groupEntrySize = isRvz ? RvzGroupEntrySize : WiaGroupEntrySize;
        uint version = BinaryPrimitives.ReadUInt32BigEndian(h1.AsSpan(4));
        uint versionCompatible = BinaryPrimitives.ReadUInt32BigEndian(h1.AsSpan(8));
        uint expectedVersion = isRvz ? RvzVersion : WiaVersion;
        uint expectedVersionReadCompatible = isRvz ? RvzVersionReadCompatible : WiaVersionReadCompatible;

        if (expectedVersion < versionCompatible || expectedVersionReadCompatible > version)
            throw new NotSupportedException($"지원하지 않는 {(isRvz ? "RVZ" : "WIA")} 버전입니다: 0x{version:X8}");

        Span<byte> digest = stackalloc byte[20];

        SHA1.HashData(h1.AsSpan(0, Header1Size - 20), digest);

        if (!digest.SequenceEqual(h1.AsSpan(Header1Size - 20, 20)))
            throw new InvalidDataException("RVZ 헤더 1 해시가 일치하지 않습니다.");

        long isoSize = (long)BinaryPrimitives.ReadUInt64BigEndian(h1.AsSpan(36));
        long declaredFileSize = (long)BinaryPrimitives.ReadUInt64BigEndian(h1.AsSpan(44));

        if (declaredFileSize != fileLength)
            throw new InvalidDataException("RVZ 파일 크기가 헤더와 다릅니다. 파일이 잘렸을 수 있습니다.");

        uint header2Size = BinaryPrimitives.ReadUInt32BigEndian(h1.AsSpan(12));

        if (header2Size < Header2MinSize || Header1Size + (long)header2Size > fileLength)
            throw new InvalidDataException("RVZ 헤더 2 크기가 올바르지 않습니다.");

        byte[] h2Raw = new byte[header2Size];

        RvzIo.ReadExactly(handle, h2Raw, Header1Size);
        SHA1.HashData(h2Raw, digest);

        if (!digest.SequenceEqual(h1.AsSpan(16, 20)))
            throw new InvalidDataException("RVZ 헤더 2 해시가 일치하지 않습니다.");

        byte[] h2 = new byte[Header2FullSize];

        Array.Copy(h2Raw, h2, Math.Min(h2Raw.Length, h2.Length));

        uint discType = BinaryPrimitives.ReadUInt32BigEndian(h2.AsSpan(0));
        uint compression = BinaryPrimitives.ReadUInt32BigEndian(h2.AsSpan(4));
        uint chunkSize = BinaryPrimitives.ReadUInt32BigEndian(h2.AsSpan(12));
        byte[] discHeader = h2.AsSpan(16, 0x80).ToArray();
        uint partitionCount = BinaryPrimitives.ReadUInt32BigEndian(h2.AsSpan(144));
        uint partitionEntrySize = BinaryPrimitives.ReadUInt32BigEndian(h2.AsSpan(148));
        long partitionOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(h2.AsSpan(152));
        ReadOnlySpan<byte> partitionHash = h2.AsSpan(160, 20);
        uint rawCount = BinaryPrimitives.ReadUInt32BigEndian(h2.AsSpan(180));
        long rawOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(h2.AsSpan(184));
        uint rawSize = BinaryPrimitives.ReadUInt32BigEndian(h2.AsSpan(192));
        uint groupCount = BinaryPrimitives.ReadUInt32BigEndian(h2.AsSpan(196));
        long groupOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(h2.AsSpan(200));
        uint groupSize = BinaryPrimitives.ReadUInt32BigEndian(h2.AsSpan(208));
        byte compressorDataSize = h2[212];

        if (compressorDataSize > 7 || header2Size < Header2MinSize + compressorDataSize)
            throw new InvalidDataException("RVZ/WIA 압축기 데이터 크기가 올바르지 않습니다.");

        byte[] compressorData = h2.AsSpan(213, compressorDataSize).ToArray();
        bool powerOfTwo = (chunkSize & (chunkSize - 1)) == 0;

        if ((chunkSize < WiiLayout.BlockTotalSize || !powerOfTwo) && chunkSize % WiiLayout.GroupTotalSize != 0)
            throw new InvalidDataException($"RVZ/WIA 청크 크기가 올바르지 않습니다: {chunkSize}");

        if (compression > (uint)RvzCompressionType.Zstd || compression == (uint)RvzCompressionType.Purge)
            throw new NotSupportedException($"지원하지 않는 RVZ/WIA 압축 방식입니다: {compression}");

        if (!isRvz && compression == (uint)RvzCompressionType.Zstd)
            throw new InvalidDataException("WIA 파일에 zstd 압축이 지정되어 있습니다.");

        var compressionType = (RvzCompressionType)compression;

        if (partitionEntrySize == 0 && partitionCount != 0)
            throw new InvalidDataException("RVZ 파티션 엔트리 크기가 0입니다.");

        long partitionBytes = (long)partitionCount * partitionEntrySize;

        if (partitionOffset < 0 || partitionBytes > fileLength || partitionOffset + partitionBytes > fileLength)
            throw new InvalidDataException("RVZ 파티션 테이블 위치가 올바르지 않습니다.");

        byte[] partitionRaw = new byte[partitionBytes];

        RvzIo.ReadExactly(handle, partitionRaw, partitionOffset);
        SHA1.HashData(partitionRaw, digest);

        if (!digest.SequenceEqual(partitionHash))
            throw new InvalidDataException("RVZ 파티션 테이블 해시가 일치하지 않습니다.");

        var partitions = new PartitionEntry[partitionCount];
        byte[] entryBuffer = new byte[PartitionEntrySize];

        for (int i = 0; i < partitions.Length; i++)
        {
            Array.Clear(entryBuffer);
            Array.Copy(partitionRaw, (long)i * partitionEntrySize, entryBuffer, 0, Math.Min((long)partitionEntrySize, PartitionEntrySize));

            partitions[i] = new PartitionEntry
            {
                Key = entryBuffer.AsSpan(0, 16).ToArray(),
                DataEntries =
                [
                    ReadPartitionData(entryBuffer.AsSpan(16, 16)),
                    ReadPartitionData(entryBuffer.AsSpan(32, 16))
                ]
            };
        }

        byte[] rawTable = ReadTable(handle, fileLength, rawOffset, rawSize, (long)rawCount * RawDataEntrySize, compressionType, compressorData);
        var rawEntries = new RawDataEntry[rawCount];

        for (int i = 0; i < rawEntries.Length; i++)
        {
            var span = rawTable.AsSpan(i * RawDataEntrySize, RawDataEntrySize);

            rawEntries[i] = new RawDataEntry((long)BinaryPrimitives.ReadUInt64BigEndian(span), (long)BinaryPrimitives.ReadUInt64BigEndian(span[8..]), BinaryPrimitives.ReadUInt32BigEndian(span[16..]), BinaryPrimitives.ReadUInt32BigEndian(span[20..]));
        }

        byte[] groupTable = ReadTable(handle, fileLength, groupOffset, groupSize, (long)groupCount * groupEntrySize, compressionType, compressorData);
        var groups = new GroupEntry[groupCount];

        for (int i = 0; i < groups.Length; i++)
        {
            var span = groupTable.AsSpan(i * groupEntrySize, groupEntrySize);
            uint packedSize = isRvz ? BinaryPrimitives.ReadUInt32BigEndian(span[8..]) : 0;

            groups[i] = new GroupEntry(BinaryPrimitives.ReadUInt32BigEndian(span), BinaryPrimitives.ReadUInt32BigEndian(span[4..]), packedSize);
        }

        return new RvzFile
        {
            Compression = compressionType,
            DiscType = discType,
            ChunkSize = chunkSize,
            IsoSize = isoSize,
            FileLength = fileLength,
            DiscHeader = discHeader,
            CompressorData = compressorData,
            Partitions = partitions,
            RawEntries = rawEntries,
            Groups = groups
        };
    }

    private static PartitionDataEntry ReadPartitionData(ReadOnlySpan<byte> span) => new(BinaryPrimitives.ReadUInt32BigEndian(span), BinaryPrimitives.ReadUInt32BigEndian(span[4..]), BinaryPrimitives.ReadUInt32BigEndian(span[8..]), BinaryPrimitives.ReadUInt32BigEndian(span[12..]));

    private static byte[] ReadTable(SafeFileHandle handle, long fileLength, long offset, long compressedSize, long decompressedSize, RvzCompressionType compression, byte[] compressorData)
    {
        if (decompressedSize == 0)
            return [];

        if (offset < 0 || compressedSize <= 0 || offset + compressedSize > fileLength)
            throw new InvalidDataException("RVZ/WIA 테이블 위치가 올바르지 않습니다.");

        byte[] compressed = new byte[compressedSize];

        RvzIo.ReadExactly(handle, compressed, offset);

        byte[] result = new byte[decompressedSize];
        using var decompressor = RvzDecompressor.Create(compression, compressorData);
        int written = decompressor.Decompress(compressed, result);

        if (written != decompressedSize)
            throw new InvalidDataException("RVZ/WIA 테이블 압축 해제 크기가 올바르지 않습니다.");

        return result;
    }
}