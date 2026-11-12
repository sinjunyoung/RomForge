using System.Buffers.Binary;
using Common.Localization;
using WiiGC.Core.Enums;
using WiiGC.Core.Models;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services;

internal sealed class RvzChunkDecoder(RvzCompressionType compression, byte[] compressorData) : IDisposable
{
    private const int HashExceptionSize = 2 + Layout.HashSize;
    private const int MaxExceptionListBytes = 52 * 64 * HashExceptionSize + 2;

    private static readonly List<HashException> EmptyList = [];

    private readonly RvzDecompressor _decompressor = RvzDecompressor.Create(compression, compressorData);
    private readonly LaggedFibonacciGenerator _generator = new();
    private readonly DecodedChunk _chunk = new();
    private byte[] _scratch = [];

    public DecodedChunk Decode(ReadOnlySpan<byte> input, bool compressedFlag, int exceptionLists, int dataSize, uint rvzPackedSize, long junkOffset)
    {
        bool compressed = compressedFlag && compression != RvzCompressionType.None;

        ReadOnlySpan<byte> stream;

        if (compressed)
        {
            long maxSize = (long)exceptionLists * MaxExceptionListBytes + (rvzPackedSize != 0 ? rvzPackedSize : dataSize);

            if (maxSize > int.MaxValue)
                throw new InvalidDataException(Loc.S.ErrRvzChunkTooLarge);

            if (_scratch.Length < maxSize)
                _scratch = new byte[maxSize];

            int written = _decompressor.Decompress(input, _scratch.AsSpan(0, (int)maxSize));

            stream = _scratch.AsSpan(0, written);
        }
        else
            stream = input;

        var lists = exceptionLists == 0 ? [] : new List<HashException>[exceptionLists];
        int position = 0;

        for (int i = 0; i < exceptionLists; i++)
        {
            if (stream.Length - position < sizeof(ushort))
                throw new InvalidDataException(Loc.S.ErrRvzHashExceptionListInvalid);

            int count = BinaryPrimitives.ReadUInt16BigEndian(stream[position..]);
            int listSize = sizeof(ushort) + count * HashExceptionSize;
            int consumed = listSize;

            if (!compressed && i == exceptionLists - 1)
                consumed = ((position + listSize + 3) & ~3) - position;

            if (stream.Length - position < consumed)
                throw new InvalidDataException(Loc.S.ErrRvzHashExceptionListInvalid);

            if (count == 0)
                lists[i] = EmptyList;
            else
            {
                var list = new List<HashException>(count);
                int entryPosition = position + sizeof(ushort);

                for (int j = 0; j < count; j++)
                {
                    ushort offset = BinaryPrimitives.ReadUInt16BigEndian(stream[entryPosition..]);
                    byte[] hash = stream.Slice(entryPosition + sizeof(ushort), Layout.HashSize).ToArray();

                    list.Add(new HashException(offset, hash));

                    entryPosition += HashExceptionSize;
                }

                lists[i] = list;
            }

            position += consumed;
        }

        var payload = stream[position..];

        if (_chunk.Data.Length < dataSize)
            _chunk.Data = new byte[dataSize];

        var data = _chunk.Data.AsSpan(0, dataSize);

        if (rvzPackedSize != 0)
        {
            if (payload.Length != rvzPackedSize)
                throw new InvalidDataException(Loc.S.ErrRvzPackSizeMismatch);

            RvzPackDecoder.Unpack(payload, data, junkOffset, _generator);
        }
        else
        {
            if (payload.Length != dataSize)
                throw new InvalidDataException(Loc.S.ErrRvzChunkSizeMismatch);

            payload.CopyTo(data);
        }

        _chunk.Length = dataSize;
        _chunk.ExceptionLists = lists;

        return _chunk;
    }

    public void Dispose() => _decompressor.Dispose();
}