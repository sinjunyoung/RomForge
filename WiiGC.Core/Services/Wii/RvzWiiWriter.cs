using Microsoft.Win32.SafeHandles;
using static WiiGC.Core.Services.RvzHeaderWriter;
using System.Buffers.Binary;
using System.Security.Cryptography;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal sealed class RvzWiiWriter
{
    private readonly record struct RawRegion(long Offset, long Size, long RewoundBase, long ExtendedSize);

    private readonly record struct GroupResult(byte[]? Buffer, int Length, uint DataSizeField, uint PackedSize);

    private sealed class Context
    {
        public byte[] Raw = [];

        public byte[] Decrypted = [];

        public byte[] Hashes = [];

        public byte[] Fresh = [];

        public byte[] Compressed = [];

        public RvzPacker Packer { get; } = new();

        public List<HashException> Exceptions { get; } = new(64);
    }

    private readonly IRvzInputSource _input;
    private readonly SafeFileHandle _output;
    private readonly int _compressionLevel;
    private readonly int _chunkSize;
    private readonly int _blocksPerChunk;
    private readonly int _chunksPerHashGroup;

    public RvzWiiWriter(IRvzInputSource input, SafeFileHandle output, int compressionLevel, int chunkSize)
    {
        ValidateCompressionLevel(compressionLevel);

        bool powerOfTwo = chunkSize > 0 && (chunkSize & (chunkSize - 1)) == 0;

        if (!powerOfTwo || chunkSize < WiiLayout.BlockTotalSize || chunkSize > WiiLayout.GroupTotalSize)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Wii 압축은 32KiB에서 2MiB 사이의 2의 거듭제곱 청크 크기만 지원합니다.");

        _input = input;
        _output = output;
        _compressionLevel = compressionLevel;
        _chunkSize = chunkSize;
        _blocksPerChunk = chunkSize / WiiLayout.BlockTotalSize;
        _chunksPerHashGroup = WiiLayout.GroupTotalSize / chunkSize;
    }

    public void Write(Action<double>? progress, CancellationToken ct)
    {
        long isoSize = _input.Length;

        if (isoSize <= DiscHeaderSize)
            throw new InvalidDataException("디스크 이미지가 너무 작습니다.");

        byte[] discHeader = new byte[DiscHeaderSize];

        _input.Read(0, discHeader);

        var partitions = WiiPartitionTable.Read(_input, isoSize);
        var rawRegions = new List<RawRegion>();
        var regionGroupInfo = new List<(uint GroupIndex, uint GroupCount)>();
        var partitionEntries = new List<(WiiPartitionSpec Spec, uint GroupIndex, uint GroupCount)>();
        uint totalGroups = 0;
        long lastEnd = 0;

        void AddRaw(long offset, long size)
        {
            long skip = offset < DiscHeaderSize ? Math.Min(DiscHeaderSize - offset, size) : 0;

            offset += skip;
            size -= skip;

            if (size <= 0)
                return;

            long blockSkip = offset % WiiLayout.BlockTotalSize;
            long rewoundBase = offset - blockSkip;
            long extendedSize = size + blockSkip;
            uint groups = (uint)((extendedSize + _chunkSize - 1) / _chunkSize);

            rawRegions.Add(new RawRegion(offset, size, rewoundBase, extendedSize));
            regionGroupInfo.Add((totalGroups, groups));

            totalGroups += groups;
        }

        foreach (var spec in partitions)
        {
            if (spec.ContainerOffset < lastEnd)
                continue;

            AddRaw(lastEnd, spec.ContainerOffset - lastEnd);
            AddRaw(spec.ContainerOffset, spec.DataStart - spec.ContainerOffset);

            long alignedBlocks = spec.DataSize / WiiLayout.BlockTotalSize;
            uint groupCount = (uint)((alignedBlocks + _blocksPerChunk - 1) / _blocksPerChunk);

            partitionEntries.Add((spec, totalGroups, groupCount));

            totalGroups += groupCount;
            lastEnd = spec.DataStart + spec.DataSize;
        }

        AddRaw(lastEnd, isoSize - lastEnd);

        var groups = new GroupEntry[totalGroups];
        long groupTableBytes = (long)totalGroups * 12;
        long partitionTableBytes = (long)partitionEntries.Count * PartitionEntrySize;
        long upperBound = EstimateUpperBound(partitionTableBytes, groupTableBytes);

        upperBound = (upperBound + WiiLayout.BlockTotalSize - 1) / WiiLayout.BlockTotalSize * WiiLayout.BlockTotalSize;

        long bytesWritten = upperBound;
        long totalWork = isoSize;
        long processed = 0;
        int window = Math.Clamp(Environment.ProcessorCount * 2, 2, 32);

        if (_input is WiaSource wiaSource)
        {
            int groupsPerChunk = (int)Math.Ceiling(wiaSource.ChunkSize / (double)WiiLayout.GroupDataSize);

            window = Math.Clamp(Environment.ProcessorCount * groupsPerChunk, window, 512);
        }

        using var compressors = new ThreadLocal<ZstdSharp.Compressor>(() => CreateCompressor(_compressionLevel), trackAllValues: true);

        void Complete((uint Index, GroupResult Result)[] results, long weight)
        {
            foreach (var (index, result) in results)
            {
                if (result.Buffer == null)
                {
                    groups[index] = new GroupEntry((uint)(bytesWritten >> 2), 0, 0);
                    continue;
                }

                if (bytesWritten >> 2 > uint.MaxValue)
                    throw new InvalidDataException("RVZ 파일이 너무 큽니다.");

                groups[index] = new GroupEntry((uint)(bytesWritten >> 2), result.DataSizeField, result.PackedSize);

                RandomAccess.Write(_output, result.Buffer.AsSpan(0, result.Length), bytesWritten);

                bytesWritten = Align4(bytesWritten + result.Length);
            }

            processed += weight;

            progress?.Invoke(Math.Min(1.0, (double)processed / totalWork) * 0.99);
        }

        try
        {
            using var pipeline = new OrderedPipeline<Context, (uint Index, GroupResult Result)[], long>(window, () => new Context(), Complete, ct);

            for (int r = 0; r < rawRegions.Count; r++)
            {
                var region = rawRegions[r];
                var (groupIndex, groupCount) = regionGroupInfo[r];

                for (uint g = 0; g < groupCount; g++)
                {
                    long offset = region.RewoundBase + (long)g * _chunkSize;
                    int length = (int)Math.Min(_chunkSize, region.RewoundBase + region.ExtendedSize - offset);
                    uint globalIndex = groupIndex + g;
                    var compressorRef = compressors;

                    pipeline.Submit(length, context =>
                    {
                        var result = ProcessRaw(context, compressorRef.Value!, offset, length);
                        return [(globalIndex, result)];
                    });
                }
            }

            foreach (var (spec, groupIndex, groupCount) in partitionEntries)
            {
                long totalBlocks = spec.DataSize / WiiLayout.BlockTotalSize;
                long numHashGroups = (totalBlocks + WiiLayout.BlocksPerGroup - 1) / WiiLayout.BlocksPerGroup;

                for (long hg = 0; hg < numHashGroups; hg++)
                {
                    long hashGroupBlockStart = hg * WiiLayout.BlocksPerGroup;
                    int blocksInThisGroup = (int)Math.Min(WiiLayout.BlocksPerGroup, totalBlocks - hashGroupBlockStart);
                    long readOffset = spec.DataStart + hashGroupBlockStart * WiiLayout.BlockTotalSize;
                    long weight = (long)blocksInThisGroup * WiiLayout.BlockTotalSize;
                    var specRef = spec;
                    var groupIndexRef = groupIndex;
                    var groupCountRef = groupCount;
                    long hashGroupBlockStartRef = hashGroupBlockStart;
                    int blocksInThisGroupRef = blocksInThisGroup;
                    var compressorRef = compressors;

                    pipeline.Submit(weight, context => ProcessPartitionHashGroup(context, compressorRef.Value!, specRef, readOffset, hashGroupBlockStartRef, blocksInThisGroupRef, totalBlocks, groupIndexRef, groupCountRef));
                }
            }

            pipeline.Drain();
        }
        finally
        {
            foreach (var compressor in compressors.Values)
                compressor.Dispose();
        }

        FinishHeaders(discHeader, isoSize, groups, rawRegions, regionGroupInfo, partitionEntries, upperBound, ct);
        progress?.Invoke(1.0);
    }

    private static GroupResult Compress(Context context, ZstdSharp.Compressor compressor, ReadOnlySpan<byte> main, uint packedSize)
    {
        int bound = ZstdSharp.Compressor.GetCompressBound(main.Length);

        if (context.Compressed.Length < bound)
            context.Compressed = new byte[bound];

        int compressedSize = compressor.Wrap(main, context.Compressed);

        if (compressedSize < main.Length)
            return new GroupResult(context.Compressed, compressedSize, (uint)compressedSize | 0x80000000u, packedSize);

        byte[] stored = main.ToArray();

        return new GroupResult(stored, main.Length, (uint)main.Length, packedSize);
    }

    private GroupResult ProcessRaw(Context context, ZstdSharp.Compressor compressor, long offset, int length)
    {
        if (context.Raw.Length < length)
            context.Raw = new byte[_chunkSize];

        var data = context.Raw.AsSpan(0, length);

        _input.Read(offset, data);

        int firstDifferent = data.IndexOfAnyExcept(data[0]);

        if (firstDifferent < 0 && data[0] == 0)
            return new GroupResult(null, 0, 0, 0);

        ReadOnlySpan<byte> main = data;
        uint packedSize = 0;

        if (firstDifferent >= 0 && context.Packer.Pack(data, offset, true))
        {
            main = context.Packer.Output;
            packedSize = context.Packer.PackedSize;
        }

        return Compress(context, compressor, main, packedSize);
    }

    private (uint, GroupResult)[] ProcessPartitionHashGroup(Context context, ZstdSharp.Compressor compressor, WiiPartitionSpec spec, long readOffset, long hashGroupBlockStart, int blocksInThisGroup, long totalBlocks, uint groupIndex, uint groupCount)
    {
        if (context.Decrypted.Length < WiiLayout.GroupDataSize)
            context.Decrypted = new byte[WiiLayout.GroupDataSize];

        var exceptionsPerChunk = new List<HashException>[_chunksPerHashGroup];

        if (_input is IWiiPartitionSource fastSource && fastSource.TryReadDecryptedHashGroup(readOffset, blocksInThisGroup, context.Decrypted, context.Exceptions))
        {
            foreach (var exception in context.Exceptions)
            {
                int globalBlock = exception.Offset / WiiLayout.BlockHeaderSize;
                int slot = exception.Offset - globalBlock * WiiLayout.BlockHeaderSize;
                int chunkLocal = globalBlock / _blocksPerChunk;
                int blockInChunk = globalBlock % _blocksPerChunk;
                var list = exceptionsPerChunk[chunkLocal] ??= [];

                list.Add(new HashException((ushort)(blockInChunk * WiiLayout.BlockHeaderSize + slot), exception.Hash));
            }

            for (int c = 0; c < _chunksPerHashGroup; c++)
                exceptionsPerChunk[c]?.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        }
        else
        {
            if (context.Raw.Length < WiiLayout.GroupTotalSize)
                context.Raw = new byte[WiiLayout.GroupTotalSize];

            if (context.Hashes.Length < WiiLayout.GroupHeaderSize)
                context.Hashes = new byte[WiiLayout.GroupHeaderSize];

            if (context.Fresh.Length < WiiLayout.GroupHeaderSize)
                context.Fresh = new byte[WiiLayout.GroupHeaderSize];

            int rawLength = blocksInThisGroup * WiiLayout.BlockTotalSize;

            _input.Read(readOffset, context.Raw.AsSpan(0, rawLength));

            using var aes = Aes.Create();
            aes.Key = spec.Key;
            Span<byte> zeroIv = stackalloc byte[16];

            Array.Clear(context.Decrypted, 0, WiiLayout.GroupDataSize);

            for (int j = 0; j < blocksInThisGroup; j++)
            {
                var block = context.Raw.AsSpan(j * WiiLayout.BlockTotalSize, WiiLayout.BlockTotalSize);
                var iv = block.Slice(WiiLayout.IvOffset, 16);

                aes.DecryptCbc(block[WiiLayout.BlockHeaderSize..], iv, context.Decrypted.AsSpan(j * WiiLayout.BlockDataSize, WiiLayout.BlockDataSize), System.Security.Cryptography.PaddingMode.None);
                aes.DecryptCbc(block[..WiiLayout.BlockHeaderSize], zeroIv, context.Hashes.AsSpan(j * WiiLayout.BlockHeaderSize, WiiLayout.BlockHeaderSize), System.Security.Cryptography.PaddingMode.None);
            }

            WiiHashTree.ComputeHashes(context.Decrypted, context.Fresh);

            for (int j = 0; j < blocksInThisGroup; j++)
            {
                int chunkLocal = j / _blocksPerChunk;
                int blockInChunk = j % _blocksPerChunk;
                var desired = context.Hashes.AsSpan(j * WiiLayout.BlockHeaderSize, WiiLayout.BlockHeaderSize);
                var computed = context.Fresh.AsSpan(j * WiiLayout.BlockHeaderSize, WiiLayout.BlockHeaderSize);

                foreach (int slot in WiiHashTree.CompareSlots())
                {
                    var a = desired.Slice(slot, WiiLayout.HashSize);
                    var b = computed.Slice(slot, WiiLayout.HashSize);

                    if (a.SequenceEqual(b))
                        continue;

                    var list = exceptionsPerChunk[chunkLocal] ??= [];
                    int offset = blockInChunk * WiiLayout.BlockHeaderSize + slot;

                    list.Add(new HashException((ushort)offset, a.ToArray()));
                }
            }
        }

        long totalDecryptedBytes = totalBlocks * WiiLayout.BlockDataSize;
        long chunkBytesEach = (long)_blocksPerChunk * WiiLayout.BlockDataSize;
        var output = new List<(uint, GroupResult)>(_chunksPerHashGroup);

        for (int c = 0; c < _chunksPerHashGroup; c++)
        {
            long globalChunkIndex = hashGroupBlockStart / _blocksPerChunk + c;

            if (globalChunkIndex >= groupCount)
                break;

            long chunkDecOffset = globalChunkIndex * chunkBytesEach;
            int realLength = (int)Math.Min(chunkBytesEach, totalDecryptedBytes - chunkDecOffset);
            var slice = context.Decrypted.AsSpan(c * (int)chunkBytesEach, realLength);
            var exceptions = exceptionsPerChunk[c] ?? EmptyExceptions;
            byte[] exceptionBytes = SerializeExceptions(exceptions);
            ReadOnlySpan<byte> main = slice;
            uint packedSize = 0;
            bool allZero = slice.IndexOfAnyExcept((byte)0) < 0;

            if (!allZero && context.Packer.Pack(slice, chunkDecOffset, true))
            {
                main = context.Packer.Output;
                packedSize = context.Packer.PackedSize;
            }

            byte[] combinedUnaligned = new byte[exceptionBytes.Length + main.Length];

            exceptionBytes.CopyTo(combinedUnaligned, 0);
            main.CopyTo(combinedUnaligned.AsSpan(exceptionBytes.Length));

            int bound = ZstdSharp.Compressor.GetCompressBound(combinedUnaligned.Length);

            if (context.Compressed.Length < bound)
                context.Compressed = new byte[bound];

            int compressedSize = compressor.Wrap(combinedUnaligned, context.Compressed);
            GroupResult result;

            if (compressedSize < combinedUnaligned.Length)
            {
                byte[] compressedCopy = new byte[compressedSize];

                context.Compressed.AsSpan(0, compressedSize).CopyTo(compressedCopy);

                result = new GroupResult(compressedCopy, compressedSize, (uint)compressedSize | 0x80000000u, packedSize);
            }
            else
            {
                int alignedExceptionLength = (exceptionBytes.Length + 3) & ~3;
                byte[] stored = new byte[alignedExceptionLength + main.Length];

                exceptionBytes.CopyTo(stored, 0);
                main.CopyTo(stored.AsSpan(alignedExceptionLength));

                result = new GroupResult(stored, stored.Length, (uint)stored.Length, packedSize);
            }

            output.Add(((uint)(groupIndex + globalChunkIndex), result));
        }

        return [.. output];
    }

    private static readonly List<HashException> EmptyExceptions = [];

    private static byte[] SerializeExceptions(List<HashException> exceptions)
    {
        byte[] bytes = new byte[2 + exceptions.Count * 22];

        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)exceptions.Count);

        int pos = 2;

        foreach (var exception in exceptions)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(pos), exception.Offset);

            exception.Hash.CopyTo(bytes.AsSpan(pos + 2));

            pos += 22;
        }

        return bytes;
    }

    private void FinishHeaders(byte[] discHeader, long isoSize, GroupEntry[] groups, List<RawRegion> rawRegions, List<(uint GroupIndex, uint GroupCount)> regionGroupInfo, List<(WiiPartitionSpec Spec, uint GroupIndex, uint GroupCount)> partitionEntries, long upperBound, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var rawEntries = new RvzRawEntry[rawRegions.Count];

        for (int i = 0; i < rawRegions.Count; i++)
            rawEntries[i] = new RvzRawEntry(rawRegions[i].Offset, rawRegions[i].Size, regionGroupInfo[i].GroupIndex, regionGroupInfo[i].GroupCount);

        byte[] partitionTable = new byte[partitionEntries.Count * PartitionEntrySize];

        for (int i = 0; i < partitionEntries.Count; i++)
        {
            var (spec, groupIndex, groupCount) = partitionEntries[i];
            var span = partitionTable.AsSpan(i * PartitionEntrySize, PartitionEntrySize);

            spec.Key.CopyTo(span);
            BinaryPrimitives.WriteUInt32BigEndian(span[16..], (uint)(spec.DataStart / WiiLayout.BlockTotalSize));
            BinaryPrimitives.WriteUInt32BigEndian(span[20..], (uint)(spec.DataSize / WiiLayout.BlockTotalSize));
            BinaryPrimitives.WriteUInt32BigEndian(span[24..], groupIndex);
            BinaryPrimitives.WriteUInt32BigEndian(span[28..], groupCount);
            BinaryPrimitives.WriteUInt32BigEndian(span[32..], 0);
            BinaryPrimitives.WriteUInt32BigEndian(span[36..], 0);
            BinaryPrimitives.WriteUInt32BigEndian(span[40..], 0);
            BinaryPrimitives.WriteUInt32BigEndian(span[44..], 0);
        }

        Finish(_output, DiscHeader.RvzTypeWii, discHeader, isoSize, _compressionLevel, _chunkSize, groups, rawEntries, partitionTable, upperBound);
    }
}