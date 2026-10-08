using Microsoft.Win32.SafeHandles;
using static WiiGC.Core.Services.RvzHeaderWriter;
using System.Buffers.Binary;
using System.Security.Cryptography;
using WiiGC.Core.Models;
using WiiGC.Core.Services.Wii;
using ZstdSharp.Unsafe;

namespace WiiGC.Core.Services.GameCube;

internal sealed class RvzGcWriter
{
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
        ValidateCompressionLevel(compressionLevel);

        if (!RvzFile.IsValidChunkSize(chunkSize))
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
        long upperBound = EstimateUpperBound(0, groupTableBytes);

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

        using var compressors = new ThreadLocal<ZstdSharp.Compressor>(() => CreateCompressor(_compressionLevel), trackAllValues: true);

        void Complete(GroupResult result, long index)
        {
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
        }

        try
        {
            using var pipeline = new OrderedPipeline<Context, GroupResult, long>(window, () => new Context(), Complete, ct);

            for (long index = 0; index < groupCount; index++)
            {
                long groupIndex = index;

                pipeline.Submit(index, context => Process(context, groupIndex, isoSize, compressors.Value!));
            }

            pipeline.Drain();
        }
        finally
        {
            foreach (var compressor in compressors.Values)
                compressor.Dispose();
        }

        ct.ThrowIfCancellationRequested();
        Finish(_output, 1, discHeader, isoSize, _compressionLevel, _chunkSize, groups, [new RvzRawEntry(DiscHeaderSize, rawSize, 0, (uint)groups.Length)], [], upperBound);
        progress?.Invoke(1.0);
    }

    private GroupResult Process(Context context, long index, long isoSize, ZstdSharp.Compressor compressor)
    {
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

    private static void ValidateGameCube(byte[] discHeader)
    {
        uint gameCube = BinaryPrimitives.ReadUInt32BigEndian(discHeader.AsSpan(0x1C));
        uint wii = BinaryPrimitives.ReadUInt32BigEndian(discHeader.AsSpan(0x18));

        if (wii == 0x5D1C9EA3)
            throw new NotSupportedException("Wii 디스크 압축은 아직 지원하지 않습니다.");

        if (gameCube != GameCubeMagic)
            throw new InvalidDataException("GameCube 디스크 이미지가 아닙니다.");
    }
}