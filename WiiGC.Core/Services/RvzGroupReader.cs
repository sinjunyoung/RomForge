using Microsoft.Win32.SafeHandles;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services;

internal readonly record struct RvzRegion(long Start, long End, int RawIndex, int PartitionIndex, int DataIndex);

internal readonly record struct RvzChunk(byte[] Data, int Length, List<HashException>[] ExceptionLists);

internal sealed class RvzGroupReader<TContext> where TContext : RvzWorkerContext
{
    private readonly RvzFile _file;
    private readonly SafeFileHandle _handle;
    private readonly Func<TContext, long, GroupEntry, int, int, long, RvzChunk> _getChunk;
    private readonly long _partitionChunkSize;
    private readonly int _partitionExceptionLists;

    public RvzGroupReader(RvzFile file, SafeFileHandle handle, Func<TContext, long, GroupEntry, int, int, long, RvzChunk> getChunk)
    {
        _file = file;
        _handle = handle;
        _getChunk = getChunk;
        _partitionChunkSize = (long)file.ChunkSize * WiiLayout.BlockDataSize / WiiLayout.BlockTotalSize;
        _partitionExceptionLists = (int)Math.Max(1, _partitionChunkSize / WiiLayout.GroupDataSize);
    }

    public RvzRegion[] BuildRegions()
    {
        var regions = new List<RvzRegion>(_file.RawEntries.Length + _file.Partitions.Sum(static p => p.DataEntries.Length));

        for (int i = 0; i < _file.RawEntries.Length; i++)
        {
            var entry = _file.RawEntries[i];

            if (entry.DataSize != 0)
                regions.Add(new RvzRegion(entry.DataOffset, entry.DataOffset + entry.DataSize, i, -1, -1));
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

                regions.Add(new RvzRegion(start, end, -1, p, d));
            }
        }

        regions.Sort(static (a, b) => a.Start.CompareTo(b.Start));

        return [.. regions];
    }

    public void ReadDecryptedGroup(TContext context, PartitionEntry partition, long groupStartSector, int validSectors)
    {
        context.Exceptions.Clear();

        long offset = groupStartSector * WiiLayout.BlockDataSize;
        long remaining = (long)validSectors * WiiLayout.BlockDataSize;
        int position = 0;
        int startEntry = FindPartitionDataEntry(partition, groupStartSector);

        if (startEntry < 0)
            throw new InvalidDataException("RVZ/WIA 파티션 데이터 엔트리를 찾을 수 없습니다.");

        for (int i = startEntry; i < partition.DataEntries.Length && remaining > 0; i++)
        {
            var entry = partition.DataEntries[i];

            if (entry.SectorCount == 0)
                continue;

            long dataOffset = ((long)entry.FirstSector - partition.FirstSector) * WiiLayout.BlockDataSize;
            long dataSize = (long)entry.SectorCount * WiiLayout.BlockDataSize;

            if (dataOffset + dataSize <= offset)
                continue;

            if (dataOffset > offset)
                throw new InvalidDataException("RVZ/WIA 데이터 영역 사이에 빈 구간이 있습니다.");

            ReadFromGroups(context, ref offset, ref remaining, context.Decrypted, ref position, _partitionChunkSize, WiiLayout.BlockDataSize, dataOffset, dataSize, entry.GroupIndex, entry.GroupCount, _partitionExceptionLists, context.Exceptions);
        }

        if (remaining != 0)
            throw new InvalidDataException("RVZ/WIA 파티션 데이터 그룹이 부족합니다.");

        if (position < context.Decrypted.Length)
            Array.Clear(context.Decrypted, position, context.Decrypted.Length - position);
    }

    private static int FindPartitionDataEntry(PartitionEntry partition, long groupStartSector)
    {
        var entries = partition.DataEntries;
        long targetSector = partition.FirstSector + groupStartSector;
        int lo = 0;
        int hi = entries.Length - 1;
        int found = -1;

        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            var entry = entries[mid];

            if (entry.SectorCount == 0 || entry.FirstSector <= targetSector)
            {
                found = mid;
                lo = mid + 1;
            }
            else
                hi = mid - 1;
        }

        while (found >= 0 && entries[found].SectorCount == 0)
            found--;

        return found;
    }

    public void ReadFromGroups(TContext context, ref long offset, ref long size, byte[] destination, ref int destinationPosition, long chunkSize, int sectorSize, long dataOffset, long dataSize, uint groupIndex, uint groupCount, int exceptionLists, List<HashException>? exceptions)
    {
        if (dataOffset + dataSize <= offset)
            return;

        if (offset < dataOffset)
            throw new InvalidDataException("RVZ/WIA 데이터 영역 사이에 빈 구간이 있습니다.");

        long skipped = dataOffset % sectorSize;

        dataOffset -= skipped;
        dataSize += skipped;

        long startGroup = (offset - dataOffset) / chunkSize;

        for (long i = startGroup; i < groupCount && size > 0; i++)
        {
            long totalGroupIndex = groupIndex + i;

            if ((ulong)totalGroupIndex >= (ulong)_file.Groups.Length)
                throw new InvalidDataException("RVZ/WIA 그룹 인덱스가 범위를 벗어났습니다.");

            var group = _file.Groups[totalGroupIndex];
            long groupOffsetInData = i * chunkSize;
            long offsetInGroup = offset - groupOffsetInData - dataOffset;
            long thisChunkSize = Math.Min(chunkSize, dataSize - groupOffsetInData);
            long bytesToRead = Math.Min(thisChunkSize - offsetInGroup, size);

            if (offsetInGroup < 0 || bytesToRead <= 0)
                throw new InvalidDataException("RVZ/WIA 그룹 오프셋이 올바르지 않습니다.");

            if (group.DataSize == 0)
                Array.Clear(destination, destinationPosition, (int)bytesToRead);
            else
            {
                var chunk = _getChunk(context, totalGroupIndex, group, (int)thisChunkSize, exceptionLists, groupOffsetInData);

                chunk.Data.AsSpan((int)offsetInGroup, (int)bytesToRead).CopyTo(destination.AsSpan(destinationPosition, (int)bytesToRead));

                if (exceptions != null && exceptionLists > 0)
                {
                    int listIndex = (int)(offsetInGroup / WiiLayout.GroupDataSize);
                    int additional = (int)(groupOffsetInData % WiiLayout.GroupDataSize / WiiLayout.BlockDataSize * WiiLayout.BlockHeaderSize);

                    if ((uint)listIndex >= (uint)chunk.ExceptionLists.Length)
                        throw new InvalidDataException("RVZ/WIA 해시 예외 목록 인덱스가 올바르지 않습니다.");

                    foreach (var exception in chunk.ExceptionLists[listIndex])
                    {
                        int adjusted = exception.Offset + additional;

                        if ((uint)adjusted > ushort.MaxValue)
                            throw new InvalidDataException("RVZ/WIA 해시 예외 오프셋이 올바르지 않습니다.");

                        exceptions.Add(new HashException((ushort)adjusted, exception.Hash));
                    }
                }
            }

            offset += bytesToRead;
            size -= bytesToRead;
            destinationPosition += (int)bytesToRead;
        }
    }

    public DecodedChunk DecodeChunk(TContext context, GroupEntry group, int dataSize, int exceptionLists, long junkOffset)
    {
        long fileOffset = group.FileOffset;
        int compressedSize = group.DataSize;

        if (fileOffset < 0 || compressedSize < 0 || fileOffset > _file.FileLength - compressedSize)
            throw new InvalidDataException("RVZ/WIA 그룹 위치가 파일 범위를 벗어났습니다.");

        context.EnsureInput(compressedSize);
        RvzIo.ReadExactly(_handle, context.Input.AsSpan(0, compressedSize), fileOffset);

        bool compressed = _file.IsGroupCompressed(group);
        uint packedSize = _file.IsRvz ? group.RvzPackedSize : 0;

        return context.Decoder.Decode(context.Input.AsSpan(0, compressedSize), compressed, exceptionLists, dataSize, packedSize, junkOffset);
    }
}
