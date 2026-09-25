using DolphinTool.Core.Models;
using Microsoft.Win32.SafeHandles;

namespace DolphinTool.Core.Rvz;

internal sealed class RvzDiscReader : IDisposable
{
    private const long RawItemTargetBytes = 0x200000;

    private enum WorkKind
    {
        Raw,
        Partition
    }

    private readonly record struct Region(long Start, long End, int RawIndex, int PartitionIndex, int DataIndex);

    private readonly record struct WorkItem(WorkKind Kind, int EntryIndex, int DataIndex, long Start, long Length, bool IsZero);

    private readonly record struct WorkResult(byte[]? Buffer, int Length, long FileOffset);

    private readonly SafeFileHandle _handle;
    private readonly RvzFile _file;

    public RvzDiscReader(string path)
    {
        _handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);

        try
        {
            _file = RvzFile.Open(_handle);

            using var probe = RvzDecompressor.Create(_file.Compression, _file.CompressorData);
        }
        catch
        {
            _handle.Dispose();

            throw;
        }
    }

    public long IsoSize => _file.IsoSize;

    public uint DiscType => _file.DiscType;

    public void WriteIso(IIsoSink output, Action<double>? progress, CancellationToken ct)
    {
        long isoSize = _file.IsoSize;

        output.SetLength(isoSize);

        int headerLength = (int)Math.Min(_file.DiscHeader.Length, isoSize);

        output.Write(0, _file.DiscHeader.AsSpan(0, headerLength));

        var reporter = new ProgressReporter(isoSize, progress);

        reporter.Add(headerLength);

        var items = BuildWorkItems(headerLength);

        RunPipeline(items, output, reporter, ct);
    }

    private List<WorkItem> BuildWorkItems(long headerLength)
    {
        var items = new List<WorkItem>();
        long chunkSize = _file.ChunkSize;
        long rawItemBytes = chunkSize >= RawItemTargetBytes ? chunkSize : RawItemTargetBytes / chunkSize * chunkSize;
        long unitSectors = Math.Max(WiiLayout.BlocksPerGroup, chunkSize / WiiLayout.BlockTotalSize);
        long cursor = headerLength;

        foreach (var region in BuildRegions())
        {
            if (region.Start != cursor)
                throw new InvalidDataException($"RVZ 데이터 영역이 연속되지 않습니다. (0x{cursor:X} → 0x{region.Start:X})");

            if (region.PartitionIndex < 0)
                AddRawItems(items, region.RawIndex, rawItemBytes);
            else
                AddPartitionItems(items, region.PartitionIndex, region.DataIndex, unitSectors);

            cursor = region.End;
        }

        if (cursor != _file.IsoSize)
            throw new InvalidDataException("RVZ 데이터가 ISO 크기만큼 채워지지 않았습니다.");

        return items;
    }

    private List<Region> BuildRegions()
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
        return regions;
    }

    private void AddRawItems(List<WorkItem> items, int entryIndex, long itemBytes)
    {
        var entry = _file.RawEntries[entryIndex];
        long alignedStart = entry.DataOffset - entry.DataOffset % WiiLayout.BlockTotalSize;
        long end = entry.DataOffset + entry.DataSize;
        long position = entry.DataOffset;

        while (position < end)
        {
            long next = Math.Min(end, alignedStart + ((position - alignedStart) / itemBytes + 1) * itemBytes);

            items.Add(new WorkItem(WorkKind.Raw, entryIndex, 0, position, next - position, IsZeroRange(entry, alignedStart, position, next)));

            position = next;
        }
    }

    private bool IsZeroRange(RawDataEntry entry, long alignedStart, long start, long end)
    {
        long chunkSize = _file.ChunkSize;
        long first = (start - alignedStart) / chunkSize;
        long last = (end - 1 - alignedStart) / chunkSize;

        if (last >= entry.GroupCount)
            return false;

        for (long i = first; i <= last; i++)
        {
            long index = entry.GroupIndex + i;

            if (index >= _file.Groups.Length || _file.Groups[index].DataSize != 0)
                return false;
        }

        return true;
    }

    private void AddPartitionItems(List<WorkItem> items, int partitionIndex, int dataIndex, long unitSectors)
    {
        var partition = _file.Partitions[partitionIndex];
        var entry = partition.DataEntries[dataIndex];
        long entryStart = (long)entry.FirstSector - partition.FirstSector;
        long entryEnd = entryStart + entry.SectorCount;
        long position = entryStart;

        while (position < entryEnd)
        {
            long next = Math.Min(entryEnd, (position / unitSectors + 1) * unitSectors);
            items.Add(new WorkItem(WorkKind.Partition, partitionIndex, dataIndex, position, next - position, false));
            position = next;
        }
    }

    private void RunPipeline(List<WorkItem> items, IIsoSink output, ProgressReporter reporter, CancellationToken ct)
    {
        int window = Math.Clamp(Environment.ProcessorCount, 2, 8);
        var contexts = new List<RvzWorkerContext>();
        var idle = new Stack<RvzWorkerContext>();
        var pending = new Queue<(Task<WorkResult> Task, RvzWorkerContext Context)>();

        void Complete((Task<WorkResult> Task, RvzWorkerContext Context) entry)
        {
            var result = entry.Task.GetAwaiter().GetResult();

            if (result.Buffer != null)
                output.Write(result.FileOffset, result.Buffer.AsSpan(0, result.Length));

            reporter.Add(result.Length);
            idle.Push(entry.Context);
        }

        try
        {
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();

                if (idle.Count == 0 && contexts.Count < window)
                {
                    var created = new RvzWorkerContext(_file.Compression, _file.CompressorData);

                    contexts.Add(created);
                    idle.Push(created);
                }

                if (idle.Count == 0)
                    Complete(pending.Dequeue());

                var context = idle.Pop();
                var work = item;

                pending.Enqueue((Task.Run(() => Process(context, work, ct), CancellationToken.None), context));
            }

            while (pending.Count > 0)
                Complete(pending.Dequeue());
        }
        finally
        {
            foreach (var entry in pending)
            {
                try
                {
                    entry.Task.Wait(ct);
                }
                catch { }
            }

            foreach (var context in contexts)
                context.Dispose();
        }
    }

    private WorkResult Process(RvzWorkerContext context, WorkItem item, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return item.Kind == WorkKind.Raw ? ProcessRaw(context, item) : ProcessPartition(context, item);
    }

    private WorkResult ProcessRaw(RvzWorkerContext context, WorkItem item)
    {
        int size = (int)item.Length;

        if (item.IsZero)
            return new WorkResult(null, size, item.Start);

        var entry = _file.RawEntries[item.EntryIndex];

        context.EnsureOutput(size);

        long offset = item.Start;
        long remaining = size;
        int position = 0;

        ReadFromGroups(context, ref offset, ref remaining, context.Output, ref position, _file.ChunkSize, WiiLayout.BlockTotalSize, entry.DataOffset, entry.DataSize, entry.GroupIndex, entry.GroupCount, 0, null);

        if (remaining != 0)
            throw new InvalidDataException("RVZ 원본 데이터 그룹이 부족합니다.");

        return new WorkResult(context.Output, size, item.Start);
    }

    private WorkResult ProcessPartition(RvzWorkerContext context, WorkItem item)
    {
        var partition = _file.Partitions[item.EntryIndex];
        long partitionSectors = partition.TotalSectors;
        long itemStart = item.Start;
        long itemEnd = item.Start + item.Length;
        int totalBytes = checked((int)(item.Length * WiiLayout.BlockTotalSize));

        context.EnsureOutput(totalBytes);

        for (long group = itemStart / WiiLayout.BlocksPerGroup; group * WiiLayout.BlocksPerGroup < itemEnd; group++)
        {
            long groupStart = group * WiiLayout.BlocksPerGroup;
            int validSectors = (int)Math.Min(WiiLayout.BlocksPerGroup, partitionSectors - groupStart);

            if (validSectors <= 0)
                throw new InvalidDataException("RVZ 파티션 섹터 범위가 올바르지 않습니다.");

            ReadDecryptedGroup(context, partition, groupStart, validSectors);
            context.Encryptor.Encrypt(partition.Key, context.Decrypted, context.Exceptions, context.Encrypted);

            long emitFrom = Math.Max(groupStart, itemStart);
            long emitTo = Math.Min(groupStart + WiiLayout.BlocksPerGroup, itemEnd);

            Buffer.BlockCopy(context.Encrypted, (int)((emitFrom - groupStart) * WiiLayout.BlockTotalSize), context.Output, (int)((emitFrom - itemStart) * WiiLayout.BlockTotalSize), (int)((emitTo - emitFrom) * WiiLayout.BlockTotalSize));
        }

        return new WorkResult(context.Output, totalBytes, ((long)partition.FirstSector + itemStart) * WiiLayout.BlockTotalSize);
    }

    private void ReadDecryptedGroup(RvzWorkerContext context, PartitionEntry partition, long groupStartSector, int validSectors)
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
            throw new InvalidDataException("RVZ 파티션 데이터 그룹이 부족합니다.");

        Array.Clear(context.Decrypted, position, context.Decrypted.Length - position);
    }

    private void ReadFromGroups(RvzWorkerContext context, ref long offset, ref long size, byte[] destination, ref int destinationPosition, long chunkSize, int sectorSize, long dataOffset, long dataSize, uint groupIndex, uint groupCount, int exceptionLists, List<HashException>? exceptions)
    {
        if (dataOffset + dataSize <= offset)
            return;

        if (offset < dataOffset)
            throw new InvalidDataException("RVZ 데이터 영역 사이에 빈 구간이 있습니다.");

        long skipped = dataOffset % sectorSize;

        dataOffset -= skipped;
        dataSize += skipped;

        long startGroup = (offset - dataOffset) / chunkSize;

        for (long i = startGroup; i < groupCount && size > 0; i++)
        {
            long totalGroupIndex = groupIndex + i;

            if (totalGroupIndex >= _file.Groups.Length)
                throw new InvalidDataException("RVZ 그룹 인덱스가 범위를 벗어났습니다.");

            var group = _file.Groups[totalGroupIndex];
            long groupOffsetInData = i * chunkSize;
            long offsetInGroup = offset - groupOffsetInData - dataOffset;
            long thisChunkSize = Math.Min(chunkSize, dataSize - groupOffsetInData);
            long bytesToRead = Math.Min(thisChunkSize - offsetInGroup, size);

            if (offsetInGroup < 0 || bytesToRead <= 0)
                throw new InvalidDataException("RVZ 그룹 오프셋이 올바르지 않습니다.");

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
                        throw new InvalidDataException("RVZ 해시 예외 목록 인덱스가 올바르지 않습니다.");

                    foreach (var exception in chunk.ExceptionLists[listIndex])
                    {
                        int adjusted = exception.Offset + additional;

                        if (adjusted > ushort.MaxValue)
                            throw new InvalidDataException("RVZ 해시 예외 오프셋이 올바르지 않습니다.");

                        exceptions.Add(new HashException((ushort)adjusted, exception.Hash));
                    }
                }
            }

            offset += bytesToRead;
            size -= bytesToRead;
            destinationPosition += (int)bytesToRead;
        }
    }

    private DecodedChunk GetChunk(RvzWorkerContext context, long totalGroupIndex, GroupEntry group, int dataSize, int exceptionLists, long junkOffset)
    {
        if (context.CachedGroupIndex == totalGroupIndex && context.CachedChunk != null)
            return context.CachedChunk;

        context.CachedGroupIndex = -1;

        long fileOffset = group.FileOffset;
        int compressedSize = group.DataSize;

        if (fileOffset + compressedSize > _file.FileLength)
            throw new InvalidDataException("RVZ 그룹 위치가 파일 범위를 벗어났습니다.");

        context.EnsureInput(compressedSize);
        RvzIo.ReadExactly(_handle, context.Input.AsSpan(0, compressedSize), fileOffset);

        context.CachedChunk = context.Decoder.Decode(context.Input.AsSpan(0, compressedSize), group.IsCompressed, exceptionLists, dataSize, group.RvzPackedSize, junkOffset);
        context.CachedGroupIndex = totalGroupIndex;

        return context.CachedChunk;
    }

    public void Dispose() => _handle.Dispose();
}