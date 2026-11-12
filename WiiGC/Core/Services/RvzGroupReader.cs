using Microsoft.Win32.SafeHandles;
using Common.Localization;
using WiiGC.Core.Models;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services;

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
        _partitionChunkSize = (long)file.ChunkSize * Layout.BlockDataSize / Layout.BlockTotalSize;
        _partitionExceptionLists = (int)Math.Max(1, _partitionChunkSize / Layout.GroupDataSize);
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

                long start = (long)entries[d].FirstSector * Layout.BlockTotalSize;
                long end = start + (long)entries[d].SectorCount * Layout.BlockTotalSize;

                regions.Add(new RvzRegion(start, end, -1, p, d));
            }
        }

        regions.Sort(static (a, b) => a.Start.CompareTo(b.Start));

        return [.. regions];
    }

    public void ReadDecryptedGroup(TContext context, PartitionEntry partition, long groupStartSector, int validSectors)
    {
        context.Exceptions.Clear();

        long offset = groupStartSector * Layout.BlockDataSize;
        long remaining = (long)validSectors * Layout.BlockDataSize;
        int position = 0;
        int startEntry = FindPartitionDataEntry(partition, groupStartSector);

        if (startEntry < 0)
            throw new InvalidDataException(Loc.S.ErrRvzPartitionDataEntryNotFound);

        for (int i = startEntry; i < partition.DataEntries.Length && remaining > 0; i++)
        {
            var entry = partition.DataEntries[i];

            if (entry.SectorCount == 0)
                continue;

            long dataOffset = ((long)entry.FirstSector - partition.FirstSector) * Layout.BlockDataSize;
            long dataSize = (long)entry.SectorCount * Layout.BlockDataSize;

            if (dataOffset + dataSize <= offset)
                continue;

            if (dataOffset > offset)
                throw new InvalidDataException(Loc.S.ErrRvzGapBetweenDataRegions);

            ReadFromGroups(context, ref offset, ref remaining, context.Decrypted, ref position, _partitionChunkSize, Layout.BlockDataSize, dataOffset, dataSize, entry.GroupIndex, entry.GroupCount, _partitionExceptionLists, context.Exceptions);
        }

        if (remaining != 0)
            throw new InvalidDataException(Loc.S.ErrRvzPartitionDataGroupsMissing);

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
            throw new InvalidDataException(Loc.S.ErrRvzGapBetweenDataRegions);

        long skipped = dataOffset % sectorSize;

        dataOffset -= skipped;
        dataSize += skipped;

        long startGroup = (offset - dataOffset) / chunkSize;

        for (long i = startGroup; i < groupCount && size > 0; i++)
        {
            long totalGroupIndex = groupIndex + i;

            if ((ulong)totalGroupIndex >= (ulong)_file.Groups.Length)
                throw new InvalidDataException(Loc.S.ErrRvzGroupIndexOutOfRange);

            var group = _file.Groups[totalGroupIndex];
            long groupOffsetInData = i * chunkSize;
            long offsetInGroup = offset - groupOffsetInData - dataOffset;
            long thisChunkSize = Math.Min(chunkSize, dataSize - groupOffsetInData);
            long bytesToRead = Math.Min(thisChunkSize - offsetInGroup, size);

            if (offsetInGroup < 0 || bytesToRead <= 0)
                throw new InvalidDataException(Loc.S.ErrRvzGroupOffsetInvalid);

            if (group.DataSize == 0)
                Array.Clear(destination, destinationPosition, (int)bytesToRead);
            else
            {
                var chunk = _getChunk(context, totalGroupIndex, group, (int)thisChunkSize, exceptionLists, groupOffsetInData);

                chunk.Data.AsSpan((int)offsetInGroup, (int)bytesToRead).CopyTo(destination.AsSpan(destinationPosition, (int)bytesToRead));

                if (exceptions != null && exceptionLists > 0)
                {
                    int listIndex = (int)(offsetInGroup / Layout.GroupDataSize);
                    int additional = (int)(groupOffsetInData % Layout.GroupDataSize / Layout.BlockDataSize * Layout.BlockHeaderSize);

                    if ((uint)listIndex >= (uint)chunk.ExceptionLists.Length)
                        throw new InvalidDataException(Loc.S.ErrRvzHashExceptionIndexInvalid);

                    foreach (var exception in chunk.ExceptionLists[listIndex])
                    {
                        int adjusted = exception.Offset + additional;

                        if ((uint)adjusted > ushort.MaxValue)
                            throw new InvalidDataException(Loc.S.ErrRvzHashExceptionOffsetInvalid);

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
            throw new InvalidDataException(Loc.S.ErrRvzGroupPositionOutOfFile);

        context.EnsureInput(compressedSize);
        RvzIo.ReadExactly(_handle, context.Input.AsSpan(0, compressedSize), fileOffset);

        bool compressed = _file.IsGroupCompressed(group);
        uint packedSize = _file.IsRvz ? group.RvzPackedSize : 0;

        return context.Decoder.Decode(context.Input.AsSpan(0, compressedSize), compressed, exceptionLists, dataSize, packedSize, junkOffset);
    }
}