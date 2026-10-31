using Common;
using K4os.Compression.LZ4;
using LibHac;
using LibHac.Fs;
using LibHac.FsSystem;
using NSW.HacPack.Models;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NSW.HacPack.Services;

public static class CompressedRomfsWriter
{
    private const int ChunkSize = 0x10000;
    private const int RawHeaderSize = 0x200;
    private const int NodeSize = 0x4000;
    private const int PhysicalAlignment = 0x10;
    private const byte TypeNone = 0;
    private const byte TypeLz4 = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct Entry
    {
        public long VirtualOffset;

        public long PhysicalOffset;

        public byte CompressionType;

        public sbyte CompressionLevel;

        public uint PhysicalSize;
    }

    public static CompressedRomfsInfo Compress(Stream input, Stream output, int compressLevel, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        long totalSize = input.Length;

        input.Position = 0;

        var entries = new List<Entry>();
        int headerLength = (int)Math.Min(RawHeaderSize, totalSize);
        byte[] headerBuffer = new byte[headerLength];

        input.ReadExactly(headerBuffer);
        output.Write(headerBuffer);
        entries.Add(new Entry
        {
            VirtualOffset = 0,
            PhysicalOffset = 0,
            CompressionType = TypeNone,
            CompressionLevel = 0,
            PhysicalSize = uint.MaxValue
        });

        long physical = headerLength;
        long virtualOffset = headerLength;
        bool canMergeNone = false;
        int batchSize = Math.Clamp(Environment.ProcessorCount * 4, 8, 64);
        var sources = new byte[batchSize][];
        var targets = new byte[batchSize][];
        var lengths = new int[batchSize];
        var encoded = new int[batchSize];

        for (int i = 0; i < batchSize; i++)
        {
            sources[i] = new byte[ChunkSize];
            targets[i] = new byte[ChunkSize];
        }

        var reporter = new ProgressReporter("RomFS 압축 중", string.Empty, totalSize, progress);
        var report = reporter.CreateAction();

        while (virtualOffset < totalSize)
        {
            ct.ThrowIfCancellationRequested();

            int count = 0;

            while (count < batchSize && virtualOffset + (long)count * ChunkSize < totalSize)
            {
                long start = virtualOffset + (long)count * ChunkSize;
                int length = (int)Math.Min(ChunkSize, totalSize - start);

                input.ReadExactly(sources[count], 0, length);

                lengths[count] = length;
                count++;
            }

            Parallel.For(0, count, i =>
            {
                int length = lengths[i];
                int size = LZ4Codec.Encode(sources[i].AsSpan(0, length), targets[i].AsSpan(0, length), (LZ4Level)compressLevel);

                encoded[i] = size > 0 && size <= length ? size : 0;
            });

            for (int i = 0; i < count; i++)
            {
                long chunkStart = virtualOffset;

                if (encoded[i] > 0)
                {
                    physical = AlignPhysical(output, physical);

                    entries.Add(new Entry
                    {
                        VirtualOffset = chunkStart,
                        PhysicalOffset = physical,
                        CompressionType = TypeLz4,
                        CompressionLevel = (sbyte)compressLevel,
                        PhysicalSize = (uint)encoded[i]
                    });

                    output.Write(targets[i], 0, encoded[i]);

                    physical += encoded[i];
                    canMergeNone = false;
                }
                else
                {
                    if (!canMergeNone)
                    {
                        physical = AlignPhysical(output, physical);

                        entries.Add(new Entry
                        {
                            VirtualOffset = chunkStart,
                            PhysicalOffset = physical,
                            CompressionType = TypeNone,
                            CompressionLevel = 0,
                            PhysicalSize = uint.MaxValue
                        });

                        canMergeNone = true;
                    }

                    output.Write(sources[i], 0, lengths[i]);

                    physical += lengths[i];
                }

                virtualOffset += lengths[i];
            }

            report(virtualOffset, totalSize);
        }

        report(totalSize, totalSize);

        long tableOffset = (physical + NodeSize - 1) / NodeSize * NodeSize;

        WriteZeros(output, tableOffset - physical);

        int entrySize = Unsafe.SizeOf<Entry>();
        long nodeStorageSize = BucketTree.QueryNodeStorageSize(NodeSize, entrySize, entries.Count);
        long entryStorageSize = BucketTree.QueryEntryStorageSize(NodeSize, entrySize, entries.Count);
        byte[] headerBytes = new byte[BucketTree.QueryHeaderStorageSize()];
        byte[] nodeBytes = new byte[nodeStorageSize];
        byte[] entryBytes = new byte[entryStorageSize];
        using var headerStorage = new ValueSubStorage(new MemoryStorage(headerBytes), 0, headerBytes.Length);
        using var nodeStorage = new ValueSubStorage(new MemoryStorage(nodeBytes), 0, nodeBytes.Length);
        using var entryStorage = new ValueSubStorage(new MemoryStorage(entryBytes), 0, entryBytes.Length);
        var builder = new BucketTree.Builder();

        builder.Initialize(new ArrayPoolMemoryResource(), in headerStorage, in nodeStorage, in entryStorage, NodeSize, entrySize, entries.Count).ThrowIfFailure();

        foreach (var entry in entries)
            builder.Add(in entry).ThrowIfFailure();

        builder.Finalize(totalSize).ThrowIfFailure();
        output.Write(nodeBytes);
        output.Write(entryBytes);

        return new CompressedRomfsInfo(tableOffset, nodeStorageSize + entryStorageSize, headerBytes, entries.Count);
    }

    private static long AlignPhysical(Stream output, long physical)
    {
        int padding = (int)(-physical & (PhysicalAlignment - 1));

        if (padding > 0)
            output.Write(new byte[padding]);

        return physical + padding;
    }

    private static void WriteZeros(Stream output, long count)
    {
        byte[] zeros = new byte[Math.Min(count, 0x10000)];

        while (count > 0)
        {
            int size = (int)Math.Min(count, zeros.Length);

            output.Write(zeros, 0, size);

            count -= size;
        }
    }
}