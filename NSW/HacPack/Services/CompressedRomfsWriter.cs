using Common.Models;
using Common.Services;
using K4os.Compression.LZ4;
using LibHac;
using LibHac.Fs;
using LibHac.FsSystem;
using NSW.HacPack.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace NSW.HacPack.Services;

public static class CompressedRomfsWriter
{
    public const int ChunkSize = 0x10000;

    private const int RawHeaderSize = 0x200;
    private const int NodeSize = 0x4000;
    private const int PhysicalAlignment = 0x10;
    private const int PipelineDepth = 4;
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

    private sealed class Batch
    {
        public readonly byte[][] Sources;
        public readonly byte[][] Targets;
        public readonly int[] Lengths;
        public readonly int[] Encoded;
        public int Count;

        public Batch(int size)
        {
            Sources = new byte[size][];
            Targets = new byte[size][];
            Lengths = new int[size];
            Encoded = new int[size];

            for (int i = 0; i < size; i++)
            {
                Sources[i] = new byte[ChunkSize];
                Targets[i] = new byte[ChunkSize];
            }
        }
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
        int batchSize = Math.Clamp(Environment.ProcessorCount * 8, 32, 128);

        var reporter = new ProgressReporter("RomFS 압축 중", string.Empty, totalSize, progress);
        var report = reporter.CreateAction();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = linked.Token;
        var failureLock = new object();
        Exception? failure = null;

        void Fail(Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                lock (failureLock)
                    failure ??= ex;
            }

            linked.Cancel();
        }

        using var free = new BlockingCollection<Batch>();
        using var readQueue = new BlockingCollection<Batch>(PipelineDepth);
        using var writeQueue = new BlockingCollection<Batch>(PipelineDepth);

        for (int i = 0; i < PipelineDepth; i++)
            free.Add(new Batch(batchSize), ct);

        var reader = Task.Factory.StartNew(() =>
        {
            try
            {
                long offset = headerLength;

                while (offset < totalSize)
                {
                    token.ThrowIfCancellationRequested();

                    var batch = free.Take(token);
                    int count = 0;

                    while (count < batchSize && offset < totalSize)
                    {
                        int length = (int)Math.Min(ChunkSize, totalSize - offset);

                        input.ReadExactly(batch.Sources[count], 0, length);

                        batch.Lengths[count] = length;
                        count++;
                        offset += length;
                    }

                    batch.Count = count;
                    readQueue.Add(batch, token);
                }

                readQueue.CompleteAdding();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        var writer = Task.Factory.StartNew(() =>
        {
            try
            {
                long virtualOffset = headerLength;
                bool canMergeNone = false;

                foreach (var batch in writeQueue.GetConsumingEnumerable(token))
                {
                    for (int i = 0; i < batch.Count; i++)
                    {
                        long chunkStart = virtualOffset;

                        if (batch.Encoded[i] > 0)
                        {
                            physical = AlignPhysical(output, physical);

                            entries.Add(new Entry
                            {
                                VirtualOffset = chunkStart,
                                PhysicalOffset = physical,
                                CompressionType = TypeLz4,
                                CompressionLevel = (sbyte)compressLevel,
                                PhysicalSize = (uint)batch.Encoded[i]
                            });
                            output.Write(batch.Targets[i], 0, batch.Encoded[i]);

                            physical += batch.Encoded[i];
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

                            output.Write(batch.Sources[i], 0, batch.Lengths[i]);

                            physical += batch.Lengths[i];
                        }

                        virtualOffset += batch.Lengths[i];
                    }

                    report(virtualOffset, totalSize);
                    free.Add(batch);
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        try
        {
            var options = new ParallelOptions { CancellationToken = token };

            foreach (var batch in readQueue.GetConsumingEnumerable(token))
            {
                Parallel.For(0, batch.Count, options, i =>
                {
                    int length = batch.Lengths[i];
                    int size = LZ4Codec.Encode(batch.Sources[i].AsSpan(0, length), batch.Targets[i].AsSpan(0, length), (LZ4Level)compressLevel);

                    batch.Encoded[i] = size > 0 && size <= length ? size : 0;
                });

                writeQueue.Add(batch, token);
            }

            writeQueue.CompleteAdding();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }

        Task.WaitAll(reader, writer);

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        ct.ThrowIfCancellationRequested();

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