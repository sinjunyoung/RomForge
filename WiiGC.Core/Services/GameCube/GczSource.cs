using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using Microsoft.Win32.SafeHandles;

namespace WiiGC.Core.Services.GameCube;

internal sealed class GczSource : IRvzInputSource
{
    private const uint Magic = 0xB10BC001;
    private const int HeaderSize = 32;
    private const ulong UncompressedFlag = 1UL << 63;
    private const int MaxBlockSize = 0x4000000;

    private readonly SafeFileHandle _handle;
    private readonly long _discSize;
    private readonly int _blockSize;
    private readonly int _blockCount;
    private readonly long _compressedDataSize;
    private readonly long _dataOffset;
    private readonly ulong[] _pointers;
    private readonly uint[] _hashes;

    public GczSource(SafeFileHandle handle)
    {
        _handle = handle;

        long fileLength = RandomAccess.GetLength(handle);
        byte[] header = new byte[HeaderSize];

        RvzIo.ReadExactly(handle, header, 0);

        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic)
            throw new InvalidDataException("GCZ 파일이 아닙니다.");

        _compressedDataSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(8));
        _discSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(16));
        _blockSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24));
        _blockCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(28));

        if (_blockCount <= 0 || _blockSize <= 0 || _blockSize > MaxBlockSize || _discSize <= 0 || _compressedDataSize < 0)
            throw new InvalidDataException("GCZ 헤더가 올바르지 않습니다.");

        if ((long)_blockCount * _blockSize < _discSize)
            throw new InvalidDataException("GCZ 블록 수가 디스크 크기보다 부족합니다.");

        long headerTotal = HeaderSize + (long)_blockCount * (sizeof(ulong) + sizeof(uint));

        if (headerTotal > fileLength)
            throw new InvalidDataException("GCZ 헤더 크기가 파일 크기보다 큽니다.");

        if (headerTotal + _compressedDataSize > fileLength)
            throw new InvalidDataException("GCZ 데이터 크기가 파일 크기보다 큽니다. 파일이 잘렸을 수 있습니다.");

        _dataOffset = headerTotal;

        byte[] pointerBytes = new byte[(long)_blockCount * sizeof(ulong)];

        RvzIo.ReadExactly(handle, pointerBytes, HeaderSize);

        byte[] hashBytes = new byte[(long)_blockCount * sizeof(uint)];

        RvzIo.ReadExactly(handle, hashBytes, HeaderSize + pointerBytes.Length);

        _pointers = new ulong[_blockCount];
        _hashes = new uint[_blockCount];

        for (int i = 0; i < _blockCount; i++)
        {
            _pointers[i] = BinaryPrimitives.ReadUInt64LittleEndian(pointerBytes.AsSpan(i * sizeof(ulong)));
            _hashes[i] = BinaryPrimitives.ReadUInt32LittleEndian(hashBytes.AsSpan(i * sizeof(uint)));
        }

        ValidatePointers();
    }

    public long Length => _discSize;

    public int BlockSize => _blockSize;

    public static bool IsGcz(SafeFileHandle handle)
    {
        if (RandomAccess.GetLength(handle) < HeaderSize)
            return false;

        Span<byte> magic = stackalloc byte[4];

        RvzIo.ReadExactly(handle, magic, 0);

        return BinaryPrimitives.ReadUInt32LittleEndian(magic) == Magic;
    }

    public void Read(long offset, Span<byte> destination)
    {
        if (offset < 0 || offset + destination.Length > _discSize)
            throw new EndOfStreamException("GCZ 범위를 벗어난 읽기입니다.");

        if (destination.Length == 0)
            return;

        int firstBlock = (int)(offset / _blockSize);
        int lastBlock = (int)((offset + destination.Length - 1) / _blockSize);
        long spanStart = BlockStart(firstBlock);
        long spanEnd = BlockEnd(lastBlock);
        int spanLength = checked((int)(spanEnd - spanStart));
        byte[] stored = ArrayPool<byte>.Shared.Rent(spanLength);
        byte[]? temporary = null;

        try
        {
            RvzIo.ReadExactly(_handle, stored.AsSpan(0, spanLength), _dataOffset + spanStart);

            for (int block = firstBlock; block <= lastBlock; block++)
            {
                int relativeStart = checked((int)(BlockStart(block) - spanStart));
                int size = checked((int)(BlockEnd(block) - BlockStart(block)));
                var blockData = stored.AsSpan(relativeStart, size);

                if (Adler32.Compute(blockData) != _hashes[block])
                    throw new InvalidDataException($"GCZ 블록 {block}의 해시가 일치하지 않습니다. 파일이 손상되었습니다.");

                long blockOffset = (long)block * _blockSize;
                int from = (int)(Math.Max(offset, blockOffset) - blockOffset);
                int to = (int)(Math.Min(offset + destination.Length, blockOffset + _blockSize) - blockOffset);
                var target = destination.Slice((int)(blockOffset + from - offset), to - from);
                bool uncompressed = (_pointers[block] & UncompressedFlag) != 0;

                if (uncompressed)
                {
                    if (size != _blockSize)
                        throw new InvalidDataException($"GCZ 블록 {block}의 비압축 크기가 올바르지 않습니다.");

                    blockData[from..to].CopyTo(target);
                    continue;
                }

                if (from == 0 && to == _blockSize)
                {
                    Inflate(stored, relativeStart, size, target, block);
                    continue;
                }

                temporary ??= ArrayPool<byte>.Shared.Rent(_blockSize);

                Inflate(stored, relativeStart, size, temporary.AsSpan(0, _blockSize), block);
                temporary.AsSpan(from, to - from).CopyTo(target);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(stored);

            if (temporary != null)
                ArrayPool<byte>.Shared.Return(temporary);
        }
    }

    private static void Inflate(byte[] source, int start, int size, Span<byte> destination, int block)
    {
        using var input = new MemoryStream(source, start, size, false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        int total = 0;

        while (total < destination.Length)
        {
            int read = zlib.Read(destination[total..]);

            if (read <= 0)
                break;

            total += read;
        }

        if (total != destination.Length)
            throw new InvalidDataException($"GCZ 블록 {block}의 압축 해제 크기가 올바르지 않습니다.");
    }

    private long BlockStart(int block) => (long)(_pointers[block] & ~UncompressedFlag);

    private long BlockEnd(int block) => block + 1 < _blockCount ? (long)(_pointers[block + 1] & ~UncompressedFlag) : _compressedDataSize;

    private void ValidatePointers()
    {
        for (int i = 0; i < _blockCount; i++)
        {
            long start = BlockStart(i);
            long end = BlockEnd(i);

            if (end > _compressedDataSize || start > _compressedDataSize || end < start)
                throw new InvalidDataException("GCZ 블록 포인터가 올바르지 않습니다.");

            bool uncompressed = (_pointers[i] & UncompressedFlag) != 0;
            long size = end - start;

            if (uncompressed && size != _blockSize)
                throw new InvalidDataException("GCZ 비압축 블록 크기가 올바르지 않습니다.");

            if (!uncompressed && size > _blockSize + 64)
                throw new InvalidDataException("GCZ 압축 블록이 너무 큽니다.");
        }
    }

    public void Dispose() => _handle.Dispose();
}