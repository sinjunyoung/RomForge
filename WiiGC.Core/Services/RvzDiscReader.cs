using Microsoft.Win32.SafeHandles;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services;

internal sealed class RvzDiscReader : IDisposable
{
    private const long RawItemTargetBytes = 0x200000;

    private enum WorkKind
    {
        Raw,
        Partition
    }

    private readonly record struct WorkItem(WorkKind Kind, int EntryIndex, int DataIndex, long Start, long Length, bool IsZero);

    private readonly record struct WorkResult(byte[]? Buffer, int Length, long FileOffset);

    private readonly SafeFileHandle _handle;
    private readonly RvzFile _file;
    private readonly RvzGroupReader<RvzWorkerContext> _groups;

    public RvzDiscReader(string path)
    {
        _handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);

        try
        {
            _file = RvzFile.Open(_handle);
            _groups = new RvzGroupReader<RvzWorkerContext>(_file, _handle, GetChunk);

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

        foreach (var region in _groups.BuildRegions())
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

        using var pipeline = new OrderedPipeline<RvzWorkerContext, WorkResult, int>(
            window,
            () => new RvzWorkerContext(_file.Compression, _file.CompressorData),
            (result, _) =>
            {
                if (result.Buffer != null)
                    output.Write(result.FileOffset, result.Buffer.AsSpan(0, result.Length));

                reporter.Add(result.Length);
            },
            ct);

        foreach (var item in items)
            pipeline.Submit(context => Process(context, item));

        pipeline.Drain();
    }

    private WorkResult Process(RvzWorkerContext context, WorkItem item) =>
        item.Kind == WorkKind.Raw ? ProcessRaw(context, item) : ProcessPartition(context, item);

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

        _groups.ReadFromGroups(context, ref offset, ref remaining, context.Output, ref position, _file.ChunkSize, WiiLayout.BlockTotalSize, entry.DataOffset, entry.DataSize, entry.GroupIndex, entry.GroupCount, 0, null);

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

            _groups.ReadDecryptedGroup(context, partition, groupStart, validSectors);
            context.Encryptor.Encrypt(partition.Key, context.Decrypted, context.Exceptions, context.Encrypted);

            long emitFrom = Math.Max(groupStart, itemStart);
            long emitTo = Math.Min(groupStart + WiiLayout.BlocksPerGroup, itemEnd);

            Buffer.BlockCopy(context.Encrypted, (int)((emitFrom - groupStart) * WiiLayout.BlockTotalSize), context.Output, (int)((emitFrom - itemStart) * WiiLayout.BlockTotalSize), (int)((emitTo - emitFrom) * WiiLayout.BlockTotalSize));
        }

        return new WorkResult(context.Output, totalBytes, ((long)partition.FirstSector + itemStart) * WiiLayout.BlockTotalSize);
    }

    private RvzChunk GetChunk(RvzWorkerContext context, long totalGroupIndex, GroupEntry group, int dataSize, int exceptionLists, long junkOffset)
    {
        if (context.CachedGroupIndex != totalGroupIndex || context.CachedChunk == null)
        {
            context.CachedGroupIndex = -1;
            context.CachedChunk = _groups.DecodeChunk(context, group, dataSize, exceptionLists, junkOffset);
            context.CachedGroupIndex = totalGroupIndex;
        }

        var chunk = context.CachedChunk;

        return new RvzChunk(chunk.Data, chunk.Length, chunk.ExceptionLists);
    }

    public void Dispose() => _handle.Dispose();
}