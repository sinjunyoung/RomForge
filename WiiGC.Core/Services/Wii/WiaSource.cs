using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal sealed class WiaSource : IRvzInputSource, IWiiPartitionSource
{
    private sealed class Context(RvzCompressionType compression, byte[] compressorData) : RvzWorkerContext(compression, compressorData)
    {
        public long LastChunkIndex = -1;
        public RvzChunk LastChunk;
        public bool HasLastChunk;
        public int CachedPartitionIndex = -1;
        public long CachedHashGroupStart = -1;
    }

    private const long ChunkCacheByteBudget = 256L * 1024 * 1024;

    private readonly SafeFileHandle _handle;
    private readonly RvzFile _file;
    private readonly RvzRegion[] _regions;
    private readonly long _headerLength;
    private readonly RvzGroupReader<Context> _groups;
    private readonly ConcurrentBag<Context> _contextPool = [];
    private readonly ConcurrentDictionary<long, Lazy<RvzChunk>> _chunkCache = new();
    private readonly ConcurrentQueue<long> _chunkCacheOrder = new();
    private long _chunkCacheBytes;

    private WiaSource(SafeFileHandle handle, RvzFile file)
    {
        _handle = handle;
        _file = file;
        _headerLength = Math.Min(file.DiscHeader.Length, file.IsoSize);
        _groups = new RvzGroupReader<Context>(file, handle, GetChunk);
        _regions = _groups.BuildRegions();
    }

    public long Length => _file.IsoSize;

    public uint ChunkSize => _file.ChunkSize;

    private Context RentContext() => _contextPool.TryTake(out var context) ? context : new Context(_file.Compression, _file.CompressorData);

    private void ReturnContext(Context context) => _contextPool.Add(context);

    public static bool IsWiaOrRvz(SafeFileHandle handle)
    {
        if (RandomAccess.GetLength(handle) < 4)
            return false;

        Span<byte> magic = stackalloc byte[4];

        RvzIo.ReadExactly(handle, magic, 0);

        return RvzMagic.IsWia(magic) || RvzMagic.IsRvz(magic);
    }

    public static WiaSource Open(SafeFileHandle handle)
    {
        try
        {
            var file = RvzFile.Open(handle);

            using (RvzDecompressor.Create(file.Compression, file.CompressorData)) { }

            return new WiaSource(handle, file);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public void Read(long offset, Span<byte> destination)
    {
        if (offset < 0 || offset > Length - destination.Length)
            throw new EndOfStreamException("WIA 범위를 벗어난 읽기입니다.");

        if (destination.Length == 0)
            return;

        var context = RentContext();

        try
        {
            int written = 0;

            while (written < destination.Length)
            {
                long current = offset + written;

                if (current < _headerLength)
                {
                    int length = (int)Math.Min(_headerLength - current, destination.Length - written);

                    _file.DiscHeader.AsSpan((int)current, length).CopyTo(destination[written..]);

                    written += length;

                    continue;
                }

                var region = FindRegion(current);
                int lengthRead = region.PartitionIndex < 0 ? ReadRaw(context, region, current, destination[written..]) : ReadPartition(context, region, current, destination[written..]);

                if (lengthRead <= 0)
                    throw new InvalidDataException("WIA 읽기 진행에 실패했습니다.");

                written += lengthRead;
            }
        }
        finally
        {
            ReturnContext(context);
        }
    }

    private RvzRegion FindRegion(long offset)
    {
        int lo = 0;
        int hi = _regions.Length - 1;
        int found = -1;

        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);

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

    private int ReadRaw(Context context, RvzRegion region, long offset, Span<byte> destination)
    {
        var entry = _file.RawEntries[region.RawIndex];
        int length = (int)Math.Min(region.End - offset, destination.Length);

        context.EnsureOutput(length);

        long readOffset = offset;
        long remaining = length;
        int position = 0;

        _groups.ReadFromGroups(context, ref readOffset, ref remaining, context.Output, ref position, _file.ChunkSize, WiiLayout.BlockTotalSize, entry.DataOffset, entry.DataSize, entry.GroupIndex, entry.GroupCount, 0, null);

        if (remaining != 0)
            throw new InvalidDataException("WIA 원본 데이터 그룹이 부족합니다.");

        context.Output.AsSpan(0, length).CopyTo(destination);

        return length;
    }

    private int ReadPartition(Context context, RvzRegion region, long offset, Span<byte> destination)
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
            _groups.ReadDecryptedGroup(context, partition, groupStartSector, validSectors);
            context.Encryptor.Encrypt(partition.Key, context.Decrypted, context.Exceptions, context.Encrypted);

            context.CachedPartitionIndex = region.PartitionIndex;
            context.CachedHashGroupStart = hashGroupStart;
        }

        int offsetInGroup = (int)(offset - (partitionStart + hashGroupStart));
        int length = (int)Math.Min(WiiLayout.GroupTotalSize - offsetInGroup, Math.Min(region.End - offset, destination.Length));

        context.Encrypted.AsSpan(offsetInGroup, length).CopyTo(destination);

        return length;
    }

    public bool TryReadDecryptedHashGroup(long readOffset, int blocksInThisGroup, byte[] decrypted, List<HashException> exceptions)
    {
        if (readOffset < _headerLength)
            return false;

        RvzRegion region;

        try
        {
            region = FindRegion(readOffset);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        if (region.PartitionIndex < 0)
            return false;

        var partition = _file.Partitions[region.PartitionIndex];
        long partitionStart = (long)partition.FirstSector * WiiLayout.BlockTotalSize;
        long relative = readOffset - partitionStart;

        if (relative < 0 || relative % WiiLayout.GroupTotalSize != 0)
            return false;

        long groupStartSector = relative / WiiLayout.BlockTotalSize;
        int validSectors = (int)Math.Min(WiiLayout.BlocksPerGroup, partition.TotalSectors - groupStartSector);

        if (validSectors != blocksInThisGroup || decrypted.Length < WiiLayout.GroupDataSize)
            return false;

        var context = RentContext();

        try
        {
            _groups.ReadDecryptedGroup(context, partition, groupStartSector, validSectors);
            context.Decrypted.AsSpan(0, WiiLayout.GroupDataSize).CopyTo(decrypted);
            exceptions.Clear();
            exceptions.AddRange(context.Exceptions);

            return true;
        }
        finally
        {
            ReturnContext(context);
        }
    }

    private RvzChunk GetChunk(Context context, long totalGroupIndex, GroupEntry group, int dataSize, int exceptionLists, long junkOffset)
    {
        if (context.HasLastChunk && context.LastChunkIndex == totalGroupIndex)
            return context.LastChunk;

        RvzChunk result;

        if (exceptionLists > 0 && dataSize <= WiiLayout.GroupDataSize)
        {
            var decoded = _groups.DecodeChunk(context, group, dataSize, exceptionLists, junkOffset);

            result = new RvzChunk(decoded.Data, decoded.Length, decoded.ExceptionLists);
        }
        else
        {
            if (!_chunkCache.TryGetValue(totalGroupIndex, out var lazy))
            {
                var newLazy = new Lazy<RvzChunk>(() => DecodeAndCache(context, totalGroupIndex, group, dataSize, exceptionLists, junkOffset), LazyThreadSafetyMode.ExecutionAndPublication);

                lazy = _chunkCache.GetOrAdd(totalGroupIndex, newLazy);
            }

            result = lazy.Value;
        }

        context.LastChunkIndex = totalGroupIndex;
        context.LastChunk = result;
        context.HasLastChunk = true;

        return result;
    }

    private RvzChunk DecodeAndCache(Context context, long totalGroupIndex, GroupEntry group, int dataSize, int exceptionLists, long junkOffset)
    {
        var decoded = _groups.DecodeChunk(context, group, dataSize, exceptionLists, junkOffset);
        var cached = new RvzChunk(decoded.Data.AsSpan(0, decoded.Length).ToArray(), decoded.Length, decoded.ExceptionLists);

        _chunkCacheOrder.Enqueue(totalGroupIndex);
        Interlocked.Add(ref _chunkCacheBytes, cached.Length);
        TrimChunkCache();

        return cached;
    }

    private void TrimChunkCache()
    {
        while (Interlocked.Read(ref _chunkCacheBytes) > ChunkCacheByteBudget && _chunkCacheOrder.TryDequeue(out var oldKey))
        {
            if (_chunkCache.TryRemove(oldKey, out var removed) && removed.IsValueCreated)
                Interlocked.Add(ref _chunkCacheBytes, -removed.Value.Length);
        }
    }

    public void Dispose()
    {
        foreach (var context in _contextPool)
            context.Dispose();

        _handle.Dispose();
    }
}