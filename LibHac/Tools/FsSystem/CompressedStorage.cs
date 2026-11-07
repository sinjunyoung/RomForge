using K4os.Compression.LZ4;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Util;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Tools.FsSystem;

internal class CompressedStorage : IStorage
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Entry
    {
        public long VirtualOffset;
        public long PhysicalOffset;
        public CompressionType CompressionType;
        public sbyte CompressionLevel;
        public uint PhysicalSize;
    }

    public static readonly int NodeSize = 0x4000;

    public static long QueryEntryStorageSize(int entryCount)
    {
        return BucketTree.QueryEntryStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
    }

    public static long QueryNodeStorageSize(int entryCount)
    {
        return BucketTree.QueryNodeStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
    }

    private readonly BucketTree _bucketTree;
    private ValueSubStorage _dataStorage;

    private readonly object _decodeLock = new();
    private byte[] _decodedBuffer;
    private long _decodedEntryOffset = -1;
    private int _decodedLength;

    public CompressedStorage()
    {
        _bucketTree = new BucketTree();
        _dataStorage = new ValueSubStorage();
    }

    public Result Initialize(MemoryResource allocatorForBucketTree, ref readonly ValueSubStorage dataStorage,
        ref readonly ValueSubStorage nodeStorage, ref readonly ValueSubStorage entryStorage, int bucketTreeEntryCount)
    {
        Result res = _bucketTree.Initialize(allocatorForBucketTree, in nodeStorage, in entryStorage, NodeSize,
            Unsafe.SizeOf<Entry>(), bucketTreeEntryCount);
        if (res.IsFailure()) return res.Miss();

        _dataStorage.Set(in dataStorage);

        return Result.Success;
    }

    public Result GetEntries(List<Entry> entries)
    {
        Result res = _bucketTree.GetOffsets(out BucketTree.Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        using BucketTree.Visitor visitor = new();

        res = _bucketTree.Find(ref visitor.Ref, 0);
        if (res.IsFailure()) return res.Miss();

        while (true)
        {
            entries.Add(visitor.Get<Entry>());

            if (!visitor.CanMoveNext())
                break;

            res = visitor.MoveNext();
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        // Validate arguments
        Result res = _bucketTree.GetOffsets(out BucketTree.Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        if (!offsets.IsInclude(offset, destination.Length))
            return ResultFs.OutOfRange.Log();

        // Find the offset in our tree
        using BucketTree.Visitor visitor = new();

        res = _bucketTree.Find(ref visitor.Ref, offset);
        if (res.IsFailure()) return res.Miss();

        long entryOffset = visitor.Get<Entry>().VirtualOffset;
        if (entryOffset < 0 || !offsets.IsInclude(entryOffset))
            return ResultFs.UnexpectedInCompressedStorageA.Log();

        // Prepare to operate in chunks
        long currentOffset = offset;
        long endOffset = offset + destination.Length;

        byte[] workBufferEnc = null;

        try
        {
            while (currentOffset < endOffset)
            {
                // Get the current entry
                Entry currentEntry = visitor.Get<Entry>();

                // Get and validate the entry's offset
                long currentEntryOffset = currentEntry.VirtualOffset;
                if (currentEntryOffset > currentOffset)
                    return ResultFs.UnexpectedInCompressedStorageA.Log();

                // Get and validate the next entry offset
                long nextEntryOffset;
                if (visitor.CanMoveNext())
                {
                    res = visitor.MoveNext();
                    if (res.IsFailure()) return res.Miss();

                    nextEntryOffset = visitor.Get<Entry>().VirtualOffset;
                    if (!offsets.IsInclude(nextEntryOffset))
                        return ResultFs.UnexpectedInCompressedStorageA.Log();
                }
                else
                {
                    nextEntryOffset = offsets.EndOffset;
                }

                if (currentOffset >= nextEntryOffset)
                    return ResultFs.UnexpectedInCompressedStorageA.Log();

                // Get the offset of the data we need in the entry 
                long dataOffsetInEntry = currentOffset - currentEntryOffset;
                long currentEntrySize = nextEntryOffset - currentEntryOffset;

                // Determine how much is left
                long remainingSize = endOffset - currentOffset;
                long toWriteSize = Math.Min(remainingSize, currentEntrySize - dataOffsetInEntry);
                Assert.SdkLessEqual(toWriteSize, destination.Length);

                Span<byte> entryDestination = destination.Slice((int)(currentOffset - offset), (int)toWriteSize);

                if (currentEntry.CompressionType == CompressionType.Lz4)
                {
                    int decodedSize = (int)currentEntrySize;

                    lock (_decodeLock)
                    {
                        if (_decodedEntryOffset != currentEntryOffset || _decodedLength != decodedSize)
                        {
                            _decodedEntryOffset = -1;

                            EnsureBufferSize(ref workBufferEnc, (int)currentEntry.PhysicalSize);

                            if (_decodedBuffer is null || _decodedBuffer.Length < decodedSize)
                                _decodedBuffer = new byte[decodedSize];

                            Span<byte> encBuffer = workBufferEnc.AsSpan(0, (int)currentEntry.PhysicalSize);

                            res = _dataStorage.Read(currentEntry.PhysicalOffset, encBuffer);
                            if (res.IsFailure()) return res.Miss();

                            Span<byte> decodeTarget = _decodedBuffer.AsSpan(0, decodedSize);

                            if (LZ4Codec.Decode(encBuffer, decodeTarget) != decodedSize)
                                Lz4.Decompress(encBuffer, decodeTarget);

                            _decodedEntryOffset = currentEntryOffset;
                            _decodedLength = decodedSize;
                        }

                        _decodedBuffer.AsSpan((int)dataOffsetInEntry, (int)toWriteSize).CopyTo(entryDestination);
                    }
                }
                else if (currentEntry.CompressionType == CompressionType.None)
                {
                    res = _dataStorage.Read(currentEntry.PhysicalOffset + dataOffsetInEntry, entryDestination);
                    if (res.IsFailure()) return res.Miss();
                }
                else if (currentEntry.CompressionType == CompressionType.Zeroed)
                {
                    entryDestination.Clear();
                }

                currentOffset += toWriteSize;
            }

            return Result.Success;
        }
        finally
        {
            if (workBufferEnc is not null)
                ArrayPool<byte>.Shared.Return(workBufferEnc);
        }

        static void EnsureBufferSize(ref byte[] buffer, int requiredSize)
        {
            if (buffer is null || buffer.Length < requiredSize)
            {
                if (buffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                buffer = ArrayPool<byte>.Shared.Rent(requiredSize);
            }

            Assert.SdkGreaterEqual(buffer.Length, requiredSize);
        }
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return ResultFs.UnsupportedWriteForCompressedStorage.Log();
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForIndirectStorage.Log();
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        Result res = _bucketTree.GetOffsets(out BucketTree.Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        size = offsets.EndOffset;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}