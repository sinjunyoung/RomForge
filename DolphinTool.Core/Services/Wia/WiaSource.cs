using DolphinTool.Core.Models;
using DolphinTool.Core.Rvz;
using Microsoft.Win32.SafeHandles;

namespace DolphinTool.Core.Services.Wia;

internal sealed class WiaSource : IRvzInputSource
{
    private readonly record struct Region(long Start, long End, int RawIndex, int PartitionIndex, int DataIndex);

    private sealed class Context(RvzCompressionType compression, byte[] compressorData) : IDisposable
    {
        public RvzChunkDecoder Decoder { get; } = new RvzChunkDecoder(compression, compressorData);

        public WiiGroupEncryptor Encryptor { get; } = new();

        public List<HashException> Exceptions { get; } = [];

        public byte[] Decrypted { get; } = new byte[WiiLayout.GroupDataSize];

        public byte[] Encrypted { get; } = new byte[WiiLayout.GroupTotalSize];

        public byte[] Input = [];

        public byte[] Output = [];

        public long CachedGroupIndex = -1;

        public DecodedChunk? CachedChunk;

        public int CachedPartitionIndex = -1;

        public long CachedHashGroupStart = -1;

        public void EnsureInput(int size)
        {
            if (Input.Length < size)
                Input = new byte[size];
        }

        public void EnsureOutput(int size)
        {
            if (Output.Length < size)
                Output = new byte[size];
        }

        public void Dispose()
        {
            Decoder.Dispose();
            Encryptor.Dispose();
        }
    }

    private readonly SafeFileHandle _handle;
    private readonly RvzFile _file;
    private readonly Region[] _regions;
    private readonly long _headerLength;
    private readonly ThreadLocal<Context> _contexts;

    private WiaSource(SafeFileHandle handle, RvzFile file)
    {
        _handle = handle;
        _file = file;
        _headerLength = Math.Min(file.DiscHeader.Length, file.IsoSize);
        _regions = BuildRegions();
        _contexts = new ThreadLocal<Context>(() => new Context(_file.Compression, _file.CompressorData), trackAllValues: true);
    }

    public long Length => _file.IsoSize;

    public static bool IsWia(SafeFileHandle handle)
    {
        if (RandomAccess.GetLength(handle) < 4)
            return false;

        Span<byte> magic = stackalloc byte[4];
        RvzIo.ReadExactly(handle, magic, 0);
        return RvzMagic.IsWia(magic);
    }

    public static WiaSource Open(SafeFileHandle handle)
    {
        try
        {
            var file = RvzFile.Open(handle);

            using (RvzDecompressor.Create(file.Compression, file.CompressorData))
            {
            }

            return new WiaSource(handle, file);
        }
        catch
        {
            handle.Dispose();

            throw;
        }
    }

    private Region[] BuildRegions()
    {
        var regions = new List<Region>();

        for (int i = 0; i < _file.RawEntries.Length; i++)
        {
            var entry = _file.RawEntries[i];

            if (entry.DataSize != 0)
                regions.Add(new Region(entry.DataOffset, entry.DataOffset + entry.DataSize, i, -1, -1));
        }

        for (int p = 0; p < _file.Partitions.Length; p++)
        {
            var entries = _file.Partitions[p].DataEntries;

            for (int d = 0; d < entries.Length; d++)
            {
                if (entries[d].SectorCount == 0)
                    continue;

                long start = (long)entries[d].FirstSector * WiiLayout.BlockTotalSize;
                long end = start + (long)entries[d].SectorCount * WiiLayout.BlockTotalSize;

                regions.Add(new Region(start, end, -1, p, d));
            }
        }

        regions.Sort((a, b) => a.Start.CompareTo(b.Start));

        return [.. regions];
    }

    public void Read(long offset, Span<byte> destination)
    {
        if (offset < 0 || offset + destination.Length > Length)
            throw new EndOfStreamException("WIA 범위를 벗어난 읽기입니다.");

        if (destination.Length == 0)
            return;

        var context = _contexts.Value!;
        int written = 0;

        while (written < destination.Length)
        {
            long current = offset + written;

            if (current < _headerLength)
            {
                int chunk = (int)Math.Min(_headerLength - current, destination.Length - written);

                _file.DiscHeader.AsSpan((int)current, chunk).CopyTo(destination.Slice(written, chunk));

                written += chunk;

                continue;
            }

            var region = FindRegion(current);

            written += region.PartitionIndex < 0 ? ReadRaw(context, region, current, destination[written..]) : ReadPartition(context, region, current, destination[written..]);
        }
    }

    private Region FindRegion(long offset)
    {
        int lo = 0, hi = _regions.Length - 1, found = -1;

        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;

            if (_regions[mid].Start <= offset)
            {
                found = mid;
                lo = mid + 1;
            }
            else
                hi = mid - 1;
        }

        if (found < 0 || offset >= _regions[found].End)
            throw new InvalidDataException("WIA 데이터 영역 사이에 빈 구간이 있습니다.");

        return _regions[found];
    }

    private int ReadRaw(Context context, Region region, long offset, Span<byte> destination)
    {
        var entry = _file.RawEntries[region.RawIndex];
        int length = (int)Math.Min(region.End - offset, destination.Length);

        context.EnsureOutput(length);

        long readOffset = offset;
        long remaining = length;
        int position = 0;

        ReadFromGroups(context, ref readOffset, ref remaining, context.Output, ref position, _file.ChunkSize, WiiLayout.BlockTotalSize, entry.DataOffset, entry.DataSize, entry.GroupIndex, entry.GroupCount, 0, null);

        if (remaining != 0)
            throw new InvalidDataException("WIA 원본 데이터 그룹이 부족합니다.");

        context.Output.AsSpan(0, length).CopyTo(destination[..length]);

        return length;
    }

    private int ReadPartition(Context context, Region region, long offset, Span<byte> destination)
    {
        var partition = _file.Partitions[region.PartitionIndex];
        long partitionStart = (long)partition.FirstSector * WiiLayout.BlockTotalSize;
        long relative = offset - partitionStart;
        long hashGroupIndex = relative / WiiLayout.GroupTotalSize;
        long hashGroupStart = hashGroupIndex * WiiLayout.GroupTotalSize;
        long groupStartSector = hashGroupIndex * WiiLayout.BlocksPerGroup;
        int validSectors = (int)Math.Min(WiiLayout.BlocksPerGroup, partition.TotalSectors - groupStartSector);

        if (validSectors <= 0)
            throw new InvalidDataException("WIA 파티션 섹터 범위가 올바르지 않습니다.");

        if (context.CachedPartitionIndex != region.PartitionIndex || context.CachedHashGroupStart != hashGroupStart)
        {
            ReadDecryptedGroup(context, partition, groupStartSector, validSectors);
            context.Encryptor.Encrypt(partition.Key, context.Decrypted, context.Exceptions, context.Encrypted);
            context.CachedPartitionIndex = region.PartitionIndex;
            context.CachedHashGroupStart = hashGroupStart;
        }

        int offsetInGroup = (int)(offset - (partitionStart + hashGroupStart));
        int length = (int)Math.Min(WiiLayout.GroupTotalSize - offsetInGroup, Math.Min(region.End - offset, destination.Length));

        context.Encrypted.AsSpan(offsetInGroup, length).CopyTo(destination[..length]);

        return length;
    }

    private void ReadDecryptedGroup(Context context, PartitionEntry partition, long groupStartSector, int validSectors)
    {
        context.Exceptions.Clear();

        long offset = groupStartSector * WiiLayout.BlockDataSize;
        long remaining = (long)validSectors * WiiLayout.BlockDataSize;
        int position = 0;
        long chunkSize = (long)_file.ChunkSize * WiiLayout.BlockDataSize / WiiLayout.BlockTotalSize;
        int exceptionLists = (int)Math.Max(1, chunkSize / WiiLayout.GroupDataSize);

        foreach (var entry in partition.DataEntries)
        {
            if (remaining == 0)
                break;

            if (entry.SectorCount == 0)
                continue;

            long dataOffset = ((long)entry.FirstSector - partition.FirstSector) * WiiLayout.BlockDataSize;
            long dataSize = (long)entry.SectorCount * WiiLayout.BlockDataSize;

            ReadFromGroups(context, ref offset, ref remaining, context.Decrypted, ref position, chunkSize, WiiLayout.BlockDataSize, dataOffset, dataSize, entry.GroupIndex, entry.GroupCount, exceptionLists, context.Exceptions);
        }

        if (remaining != 0)
            throw new InvalidDataException("WIA 파티션 데이터 그룹이 부족합니다.");

        Array.Clear(context.Decrypted, position, context.Decrypted.Length - position);
    }

    private void ReadFromGroups(Context context, ref long offset, ref long size, byte[] destination, ref int destinationPosition, long chunkSize, int sectorSize, long dataOffset, long dataSize, uint groupIndex, uint groupCount, int exceptionLists, List<HashException>? exceptions)
    {
        if (dataOffset + dataSize <= offset)
            return;

        if (offset < dataOffset)
            throw new InvalidDataException("WIA 데이터 영역 사이에 빈 구간이 있습니다.");

        long skipped = dataOffset % sectorSize;

        dataOffset -= skipped;
        dataSize += skipped;

        long startGroup = (offset - dataOffset) / chunkSize;

        for (long i = startGroup; i < groupCount && size > 0; i++)
        {
            long totalGroupIndex = groupIndex + i;

            if (totalGroupIndex >= _file.Groups.Length)
                throw new InvalidDataException("WIA 그룹 인덱스가 범위를 벗어났습니다.");

            var group = _file.Groups[totalGroupIndex];
            long groupOffsetInData = i * chunkSize;
            long offsetInGroup = offset - groupOffsetInData - dataOffset;
            long thisChunkSize = Math.Min(chunkSize, dataSize - groupOffsetInData);
            long bytesToRead = Math.Min(thisChunkSize - offsetInGroup, size);

            if (offsetInGroup < 0 || bytesToRead <= 0)
                throw new InvalidDataException("WIA 그룹 오프셋이 올바르지 않습니다.");

            if (group.DataSize == 0)
                Array.Clear(destination, destinationPosition, (int)bytesToRead);
            else
            {
                var chunk = GetChunk(context, totalGroupIndex, group, (int)thisChunkSize, exceptionLists, groupOffsetInData);

                Buffer.BlockCopy(chunk.Data, (int)offsetInGroup, destination, destinationPosition, (int)bytesToRead);

                if (exceptions != null && exceptionLists > 0)
                {
                    int listIndex = (int)(offsetInGroup / WiiLayout.GroupDataSize);
                    int additional = (int)(groupOffsetInData % WiiLayout.GroupDataSize / WiiLayout.BlockDataSize * WiiLayout.BlockHeaderSize);

                    if (listIndex >= chunk.ExceptionLists.Length)
                        throw new InvalidDataException("WIA 해시 예외 목록 인덱스가 올바르지 않습니다.");

                    foreach (var exception in chunk.ExceptionLists[listIndex])
                    {
                        int adjusted = exception.Offset + additional;

                        if (adjusted > ushort.MaxValue)
                            throw new InvalidDataException("WIA 해시 예외 오프셋이 올바르지 않습니다.");

                        exceptions.Add(new HashException((ushort)adjusted, exception.Hash));
                    }
                }
            }

            offset += bytesToRead;
            size -= bytesToRead;
            destinationPosition += (int)bytesToRead;
        }
    }

    private DecodedChunk GetChunk(Context context, long totalGroupIndex, GroupEntry group, int dataSize, int exceptionLists, long junkOffset)
    {
        if (context.CachedGroupIndex == totalGroupIndex && context.CachedChunk != null)
            return context.CachedChunk;

        context.CachedGroupIndex = -1;

        long fileOffset = group.FileOffset;
        int compressedSize = group.DataSize;

        if (fileOffset + compressedSize > _file.FileLength)
            throw new InvalidDataException("WIA 그룹 위치가 파일 범위를 벗어났습니다.");

        context.EnsureInput(compressedSize);
        RvzIo.ReadExactly(_handle, context.Input.AsSpan(0, compressedSize), fileOffset);

        var chunk = context.Decoder.Decode(context.Input.AsSpan(0, compressedSize), _file.Compression != RvzCompressionType.None, exceptionLists, dataSize, 0, junkOffset); 

        context.CachedChunk = chunk;
        context.CachedGroupIndex = totalGroupIndex;

        return chunk;
    }

    public void Dispose()
    {
        foreach (var context in _contexts.Values)
            context.Dispose();

        _contexts.Dispose();
        _handle.Dispose();
    }
}