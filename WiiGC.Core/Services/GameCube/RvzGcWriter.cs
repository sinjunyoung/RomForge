using Microsoft.Win32.SafeHandles;
using System.Buffers.Binary;
using System.Security.Cryptography;
using WiiGC.Core.Models;
using WiiGC.Core.Services.Wii;
using ZstdSharp.Unsafe;

namespace WiiGC.Core.Services.GameCube;

internal sealed class RvzGcWriter
{
    private const int DiscHeaderSize = 0x80;
    private const int Header1Size = 0x48;
    private const int Header2Size = 0xDC;
    private const uint RvzVersion = 0x01000000;
    private const uint RvzVersionWriteCompatible = 0x00030000;
    private const uint GameCubeMagic = 0xC2339F3D;

    private readonly record struct GroupResult(byte[]? Buffer, int Length, uint DataSizeField, uint PackedSize, long InputLength, int ReuseValue);

    private sealed class Context
    {
        public byte[] Input = [];
        public byte[] Compressed = [];
        public RvzPacker Packer { get; } = new();
    }

    private readonly IRvzInputSource _input;
    private readonly SafeFileHandle _output;
    private readonly int _compressionLevel;
    private readonly int _chunkSize;

    public RvzGcWriter(IRvzInputSource input, SafeFileHandle output, int compressionLevel, int chunkSize)
    {
        if (compressionLevel < ZstdSharp.Compressor.MinCompressionLevel || compressionLevel > ZstdSharp.Compressor.MaxCompressionLevel)
            throw new ArgumentOutOfRangeException(nameof(compressionLevel), "zstd 압축 레벨이 범위를 벗어났습니다.");

        bool powerOfTwo = (chunkSize & (chunkSize - 1)) == 0;

        if ((chunkSize < WiiLayout.BlockTotalSize || !powerOfTwo) && chunkSize % WiiLayout.GroupTotalSize != 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "RVZ 청크 크기가 올바르지 않습니다.");

        _input = input;
        _output = output;
        _compressionLevel = compressionLevel;
        _chunkSize = chunkSize;
    }

    public void Write(Action<double>? progress, CancellationToken ct)
    {
        long isoSize = _input.Length;

        if (isoSize <= DiscHeaderSize)
            throw new InvalidDataException("디스크 이미지가 너무 작습니다.");

        byte[] discHeader = new byte[DiscHeaderSize];

        _input.Read(0, discHeader);
        ValidateGameCube(discHeader);

        long groupCount = (isoSize + _chunkSize - 1) / _chunkSize;

        if (groupCount > uint.MaxValue)
            throw new InvalidDataException("그룹 수가 너무 많습니다.");

        long rawSize = isoSize - DiscHeaderSize;
        long groupTableBytes = groupCount * 12;
        long upperBound = Header1Size + Header2Size + 24 + 0x100 + groupTableBytes * 9 / 16;

        upperBound = (upperBound + WiiLayout.BlockTotalSize - 1) / WiiLayout.BlockTotalSize * WiiLayout.BlockTotalSize;

        var groups = new GroupEntry[groupCount];
        var reusable = new Dictionary<(long Size, int Value), GroupEntry>();
        long bytesWritten = upperBound;
        long processed = 0;
        int window = Math.Clamp(Environment.ProcessorCount * 2, 2, 32);

        if (_input is WiaSource wiaSource)
        {
            int groupsPerChunk = (int)Math.Ceiling(wiaSource.ChunkSize / (double)_chunkSize);
            window = Math.Clamp(Environment.ProcessorCount * groupsPerChunk, window, 4096);
        }

        var contexts = new List<Context>();
        var idle = new Stack<Context>();
        var pending = new Queue<(Task<GroupResult> Task, Context Context, long Index)>();
        using var compressors = new ThreadLocal<ZstdSharp.Compressor>(CreateCompressor, trackAllValues: true);

        void Complete((Task<GroupResult> Task, Context Context, long Index) entry)
        {
            var result = entry.Task.GetAwaiter().GetResult();
            long index = entry.Index;
            long chunkLength = result.InputLength;

            if (result.ReuseValue >= 0 && reusable.TryGetValue((chunkLength, result.ReuseValue), out var existing))
                groups[index] = existing;
            else if (result.ReuseValue == 0)
            {
                var zero = new GroupEntry((uint)(bytesWritten >> 2), 0, 0);

                groups[index] = zero;
                reusable[(chunkLength, 0)] = zero;
            }
            else
            {
                if (bytesWritten >> 2 > uint.MaxValue)
                    throw new InvalidDataException("RVZ 파일이 너무 큽니다.");

                var entryValue = new GroupEntry((uint)(bytesWritten >> 2), result.DataSizeField, result.PackedSize);

                RandomAccess.Write(_output, result.Buffer.AsSpan(0, result.Length), bytesWritten);
                bytesWritten = Align4(bytesWritten + result.Length);

                groups[index] = entryValue;

                if (result.ReuseValue >= 0)
                    reusable[(chunkLength, result.ReuseValue)] = entryValue;
            }

            processed += chunkLength;

            progress?.Invoke(Math.Min(1.0, (double)processed / isoSize) * 0.99);
            idle.Push(entry.Context);
        }

        try
        {
            for (long index = 0; index < groupCount; index++)
            {
                ct.ThrowIfCancellationRequested();

                if (idle.Count == 0 && contexts.Count < window)
                {
                    var created = new Context();

                    contexts.Add(created);
                    idle.Push(created);
                }

                if (idle.Count == 0)
                    Complete(pending.Dequeue());

                var context = idle.Pop();
                long groupIndex = index;

                pending.Enqueue((Task.Run(() => Process(context, groupIndex, isoSize, compressors.Value!, ct), CancellationToken.None), context, index));
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

            foreach (var compressor in compressors.Values)
                compressor.Dispose();
        }

        FinishHeaders(discHeader, isoSize, rawSize, groups, upperBound, ct);
        progress?.Invoke(1.0);
    }

    private ZstdSharp.Compressor CreateCompressor()
    {
        var compressor = new ZstdSharp.Compressor(_compressionLevel);

        compressor.SetParameter(ZSTD_cParameter.ZSTD_c_contentSizeFlag, 0);

        return compressor;
    }

    private GroupResult Process(Context context, long index, long isoSize, ZstdSharp.Compressor compressor, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        long offset = index * _chunkSize;
        int length = (int)Math.Min(_chunkSize, isoSize - offset);

        if (context.Input.Length < length)
            context.Input = new byte[_chunkSize];

        var data = context.Input.AsSpan(0, length);

        _input.Read(offset, data);

        int firstDifferent = data.IndexOfAnyExcept(data[0]);
        int reuseValue = firstDifferent < 0 ? data[0] : -1;

        if (reuseValue == 0)
            return new GroupResult(null, 0, 0, 0, length, 0);

        ReadOnlySpan<byte> main = data;
        uint packedSize = 0;

        if (reuseValue < 0 && context.Packer.Pack(data, offset, true))
        {
            main = context.Packer.Output;
            packedSize = context.Packer.PackedSize;
        }

        int bound = ZstdSharp.Compressor.GetCompressBound(main.Length);

        if (context.Compressed.Length < bound)
            context.Compressed = new byte[bound];

        int compressedSize = compressor.Wrap(main, context.Compressed);

        if (compressedSize < main.Length)
            return new GroupResult(context.Compressed, compressedSize, (uint)compressedSize | 0x80000000u, packedSize, length, reuseValue);

        byte[] stored = packedSize != 0 ? context.Packer.OutputBuffer : context.Input;

        return new GroupResult(stored, main.Length, (uint)main.Length, packedSize, length, reuseValue);
    }

    private void FinishHeaders(byte[] discHeader, long isoSize, long rawSize, GroupEntry[] groups, long upperBound, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        byte[] rawTable = new byte[24];

        BinaryPrimitives.WriteUInt64BigEndian(rawTable.AsSpan(0), DiscHeaderSize);
        BinaryPrimitives.WriteUInt64BigEndian(rawTable.AsSpan(8), (ulong)rawSize);
        BinaryPrimitives.WriteUInt32BigEndian(rawTable.AsSpan(16), 0);
        BinaryPrimitives.WriteUInt32BigEndian(rawTable.AsSpan(20), (uint)groups.Length);

        byte[] groupTable = new byte[groups.Length * 12];

        for (int i = 0; i < groups.Length; i++)
        {
            var span = groupTable.AsSpan(i * 12, 12);

            BinaryPrimitives.WriteUInt32BigEndian(span, groups[i].DataOffset4);
            BinaryPrimitives.WriteUInt32BigEndian(span[4..], groups[i].DataSizeField);
            BinaryPrimitives.WriteUInt32BigEndian(span[8..], groups[i].RvzPackedSize);
        }

        using var compressor = CreateCompressor();
        byte[] compressedRaw = CompressTable(compressor, rawTable);
        byte[] compressedGroups = CompressTable(compressor, groupTable);
        long cursor = Header1Size + Header2Size;
        long partitionOffset = WriteTable([], ref cursor, upperBound);
        long rawOffset = WriteTable(compressedRaw, ref cursor, upperBound);
        long groupOffset = WriteTable(compressedGroups, ref cursor, upperBound);
        byte[] header2 = new byte[Header2Size];

        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(0), 1);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(4), (uint)RvzCompressionType.Zstd);
        BinaryPrimitives.WriteInt32BigEndian(header2.AsSpan(8), _compressionLevel);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(12), (uint)_chunkSize);
        discHeader.CopyTo(header2.AsSpan(16));
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(144), 0);
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(148), 0x30);
        BinaryPrimitives.WriteUInt64BigEndian(header2.AsSpan(152), (ulong)partitionOffset);
        SHA1.HashData([], header2.AsSpan(160, 20));
        BinaryPrimitives.WriteUInt32BigEndian(header2.AsSpan(180), 1);
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

    private static void ValidateGameCube(byte[] discHeader)
    {
        uint gameCube = BinaryPrimitives.ReadUInt32BigEndian(discHeader.AsSpan(0x1C));
        uint wii = BinaryPrimitives.ReadUInt32BigEndian(discHeader.AsSpan(0x18));

        if (wii == 0x5D1C9EA3)
            throw new NotSupportedException("Wii 디스크 압축은 아직 지원하지 않습니다.");

        if (gameCube != GameCubeMagic)
            throw new InvalidDataException("GameCube 디스크 이미지가 아닙니다.");
    }

    private static long Align4(long value) => (value + 3) & ~3L;
}