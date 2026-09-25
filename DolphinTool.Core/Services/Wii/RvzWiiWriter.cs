using DolphinTool.Core.Models;
using DolphinTool.Core.Rvz;
using Microsoft.Win32.SafeHandles;
using System.Buffers.Binary;
using System.Security.Cryptography;
using ZstdSharp.Unsafe;

namespace DolphinTool.Core.Services.Wii;

internal sealed class RvzWiiWriter
{
    private const int DiscHeaderSize = 0x80;
    private const int Header1Size = 0x48;
    private const int Header2Size = 0xDC;
    private const int PartitionEntrySize = 0x30;
    private const uint RvzVersion = 0x01000000;
    private const uint RvzVersionWriteCompatible = 0x00030000;
    private const uint WiiMagic = 0x5D1C9EA3;

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
        if (compressionLevel < ZstdSharp.Compressor.MinCompressionLevel || compressionLevel > ZstdSharp.Compressor.MaxCompressionLevel)
            throw new ArgumentOutOfRangeException(nameof(compressionLevel), "zstd 압축 레벨이 범위를 벗어났습니다.");

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
        long upperBound = Header1Size + Header2Size + partitionTableBytes + 24 + 0x100 + groupTableBytes * 9 / 16;
        upperBound = (upperBound + WiiLayout.BlockTotalSize - 1) / WiiLayout.BlockTotalSize * WiiLayout.BlockTotalSize;

        long bytesWritten = upperBound;
        long totalWork = isoSize;
        long processed = 0;

        int window = Math.Clamp(Environment.ProcessorCount * 2, 2, 32);
        var contexts = new List<Context>();
        var idle = new Stack<Context>();
        var pending = new Queue<(Task<(uint Index, GroupResult Result)[]> Task, Context Context, long Weight)>();
        using var compressors = new ThreadLocal<ZstdSharp.Compressor>(CreateCompressor, trackAllValues: true);

        void Complete((Task<(uint Index, GroupResult Result)[]> Task, Context Context, long Weight) entry)
        {
            var results = entry.Task.GetAwaiter().GetResult();

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

            processed += entry.Weight;
            progress?.Invoke(Math.Min(1.0, (double)processed / totalWork) * 0.99);
            idle.Push(entry.Context);
        }

        void Enqueue(Func<Context, (uint, GroupResult)[]> work, long weight)
        {
            if (idle.Count == 0 && contexts.Count < window)
            {
                var created = new Context();
                contexts.Add(created);
                idle.Push(created);
            }

            if (idle.Count == 0)
                Complete(pending.Dequeue());

            var context = idle.Pop();
            pending.Enqueue((Task.Run(() => work(context), CancellationToken.None), context, weight));
        }

        try
        {
            for (int r = 0; r < rawRegions.Count; r++)
            {
                var region = rawRegions[r];
                var (groupIndex, groupCount) = regionGroupInfo[r];

                for (uint g = 0; g < groupCount; g++)
                {
                    ct.ThrowIfCancellationRequested();

                    long offset = region.RewoundBase + (long)g * _chunkSize;
                    int length = (int)Math.Min(_chunkSize, region.RewoundBase + region.ExtendedSize - offset);
                    uint globalIndex = groupIndex + g;

                    var compressorRef = compressors;
                    Enqueue(context =>
                    {
                        var result = ProcessRaw(context, compressorRef.Value!, offset, length);
                        return [(globalIndex, result)];
                    }, length);
                }
            }

            foreach (var (spec, groupIndex, groupCount) in partitionEntries)
            {
                long totalBlocks = spec.DataSize / WiiLayout.BlockTotalSize;
                long numHashGroups = (totalBlocks + WiiLayout.BlocksPerGroup - 1) / WiiLayout.BlocksPerGroup;

                for (long hg = 0; hg < numHashGroups; hg++)
                {
                    ct.ThrowIfCancellationRequested();

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

                    Enqueue(context => ProcessPartitionHashGroup(context, compressorRef.Value!, specRef, readOffset,
                        hashGroupBlockStartRef, blocksInThisGroupRef, totalBlocks, groupIndexRef, groupCountRef), weight);
                }
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
                catch
                {
                }
            }

            foreach (var compressor in compressors.Values)
                compressor.Dispose();
        }

        FinishHeaders(discHeader, isoSize, groups, rawRegions, regionGroupInfo, partitionEntries, upperBound, ct);
        progress?.Invoke(1.0);
    }

    private ZstdSharp.Compressor CreateCompressor()
    {
        var compressor = new ZstdSharp.Compressor(_compressionLevel);
        compressor.SetParameter(ZSTD_cParameter.ZSTD_c_contentSizeFlag, 0);
        return compressor;
    }

    private GroupResult Compress(Context context, ZstdSharp.Compressor compressor, ReadOnlySpan<byte> main, uint packedSize)
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

    private (uint, GroupResult)[] ProcessPartitionHashGroup(Context context, ZstdSharp.Compressor compressor,
        WiiPartitionSpec spec, long readOffset, long hashGroupBlockStart, int blocksInThisGroup, long totalBlocks,
        uint groupIndex, uint groupCount)
    {
        if (context.Raw.Length < WiiLayout.GroupTotalSize)
            context.Raw = new byte[WiiLayout.GroupTotalSize];
        if (context.Decrypted.Length < WiiLayout.GroupDataSize)
            context.Decrypted = new byte[WiiLayout.GroupDataSize];
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
            var iv = block.Slice(0x3D0, 16);
            aes.DecryptCbc(block[WiiLayout.BlockHeaderSize..], iv, context.Decrypted.AsSpan(j * WiiLayout.BlockDataSize, WiiLayout.BlockDataSize), System.Security.Cryptography.PaddingMode.None);
            aes.DecryptCbc(block[..WiiLayout.BlockHeaderSize], zeroIv, context.Hashes.AsSpan(j * WiiLayout.BlockHeaderSize, WiiLayout.BlockHeaderSize), System.Security.Cryptography.PaddingMode.None);
        }

        WiiHashTree.ComputeHashes(context.Decrypted, context.Fresh);

        var exceptionsPerChunk = new List<HashException>[_chunksPerHashGroup];

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

                var list = exceptionsPerChunk[chunkLocal] ??= new List<HashException>();
                int offset = blockInChunk * WiiLayout.BlockHeaderSize + slot;
                list.Add(new HashException((ushort)offset, a.ToArray()));
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

        return output.ToArray();
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

    private void FinishHeaders(byte[] discHeader, long isoSize, GroupEntry[] groups,
        List<RawRegion> rawRegions, List<(uint GroupIndex, uint GroupCount)> regionGroupInfo,
        List<(WiiPartitionSpec Spec, uint GroupIndex, uint GroupCount)> partitionEntries, long upperBound, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        byte[] rawTable = new byte[rawRegions.Count * 24];
        for (int i = 0; i < rawRegions.Count; i++)
        {
            var region = rawRegions[i];
            var (groupIndex, groupCount) = regionGroupInfo[i];
            var span = rawTable.AsSpan(i * 24, 24);
            BinaryPrimitives.WriteUInt64BigEndian(span, (ulong)region.Offset);
            BinaryPrimitives.WriteUInt64BigEndian(span[8..], (ulong)region.Size);
            BinaryPrimitives.WriteUInt32BigEndian(span[16..], groupIndex);
            BinaryPrimitives.WriteUInt32BigEndian(span[20..], groupCount);
        }

        byte[] groupTable = new byte[groups.Length * 12];
        for (int i = 0; i < groups.Length; i++)
        {
            var span = groupTable.AsSpan(i * 12, 12);
            BinaryPrimitives.WriteUInt32BigEndian(span, groups[i].DataOffset4);
            BinaryPrimitives.WriteUInt32BigEndian(span[4..], groups[i].DataSizeField);
            BinaryPrimitives.WriteUInt32BigEndian(span[8..], groups[i].RvzPackedSize);
        }

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

        using var compressor = CreateCompressor();
        byte[] compressedRaw = CompressTable(compressor, rawTable);
        byte[] compressedGroups = CompressTable(compressor, groupTable);

        long cursor = Header1Size + Header2Size;
        long partitionOffset = WriteTable(partitionTable, ref cursor, upperBound);
        long rawOffset = WriteTable(compressedRaw, ref cursor, upperBound);
        long groupOffset = WriteTable(compressedGroups, ref cursor, upperBound);

        byte[] header2 = new byte[Header2Size];
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(0), 2);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(4), (uint)RvzCompressionType.Zstd);
        BinaryPrimitives.WriteInt32BigEndian(header2.AsSpan(8), _compressionLevel);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(12), (uint)_chunkSize);
        discHeader.CopyTo(header2.AsSpan(16));
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(144), (uint)partitionEntries.Count);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(148), PartitionEntrySize);
        BinaryPrimitives.WriteUInt64BigEndian(header2.AsSpan(152), (ulong)partitionOffset);
        SHA1.HashData(partitionTable, header2.AsSpan(160, 20));
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(180), (uint)rawRegions.Count);
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
        BinaryPrimitives.WriteUInt64BigEndian(header1.AsSpan(44), (ulong)RandomAccess.GetLength(_output));
        SHA1.HashData(header1.AsSpan(0, Header1Size - 20), header1.AsSpan(Header1Size - 20, 20));

        RandomAccess.Write(_output, header1, 0);
        RandomAccess.Write(_output, header2, Header1Size);
    }

    private static byte[] CompressTable(ZstdSharp.Compressor compressor, byte[] table)
    {
        byte[] buffer = new byte[ZstdSharp.Compressor.GetCompressBound(table.Length)];
        int written = compressor.Wrap(table, buffer);
        return buffer.AsSpan(0, written).ToArray();
    }

    private long WriteTable(byte[] data, ref long cursor, long upperBound)
    {
        if (cursor <= upperBound && cursor + data.Length > upperBound)
            cursor = Align4(RandomAccess.GetLength(_output));

        long offset = cursor;
        RandomAccess.Write(_output, data, cursor);
        cursor = Align4(cursor + data.Length);
        return offset;
    }

    private static long Align4(long value) => (value + 3) & ~3L;

    public static bool IsWii(ReadOnlySpan<byte> discHeader) => BinaryPrimitives.ReadUInt32BigEndian(discHeader[0x18..]) == WiiMagic;
}
