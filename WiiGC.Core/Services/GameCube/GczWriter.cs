using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using Microsoft.Win32.SafeHandles;
using WiiGC.Core.Services;

namespace WiiGC.Core.Services.GameCube;

internal sealed class GczWriter : IDisposable
{
    public const int DefaultBlockSize = 0x20000;

    private const uint Magic = 0xB10BC001;
    private const int HeaderSize = 32;
    private const int MinBlockSize = 0x800;
    private const int MaxBlockSize = 0x4000000;
    private const ulong UncompressedFlag = 1UL << 63;

    private readonly record struct BlockResult(byte[] Buffer, int Length, bool Stored, uint Hash, byte[]? Rented);

    private readonly SafeFileHandle _output;
    private readonly long _discSize;
    private readonly int _blockSize;
    private readonly uint _subType;
    private readonly int _blockCount;
    private readonly long _dataOffset;
    private readonly ulong[] _pointers;
    private readonly uint[] _hashes;
    private readonly OrderedPipeline<object, BlockResult, int> _pipeline;
    private byte[]? _current;
    private int _currentLength;
    private long _appended;
    private int _completed;
    private long _dataPosition;
    private BlockResult? _zeroBlock;

    public GczWriter(SafeFileHandle output, long discSize, int blockSize, uint subType)
    {
        if (blockSize < MinBlockSize || blockSize > MaxBlockSize || (blockSize & (blockSize - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "GCZ 블록 크기는 2의 거듭제곱이어야 합니다.");

        if (discSize <= 0)
            throw new InvalidDataException("디스크 이미지가 비어 있습니다.");

        long blockCount = (discSize + blockSize - 1) / blockSize;

        if (blockCount > int.MaxValue / 16)
            throw new InvalidDataException("GCZ 블록 수가 너무 많습니다.");

        _output = output;
        _discSize = discSize;
        _blockSize = blockSize;
        _subType = subType;
        _blockCount = (int)blockCount;
        _dataOffset = HeaderSize + (long)_blockCount * (sizeof(ulong) + sizeof(uint));
        _pointers = new ulong[_blockCount];
        _hashes = new uint[_blockCount];
        _pipeline = new OrderedPipeline<object, BlockResult, int>(Math.Clamp(Environment.ProcessorCount * 4, 4, 64), () => new object(), CompleteBlock, CancellationToken.None);
    }

    public void Append(ReadOnlySpan<byte> data)
    {
        if (_appended + data.Length > _discSize)
            throw new InvalidDataException("GCZ에 디스크 크기보다 많은 데이터가 전달되었습니다.");

        while (data.Length > 0)
        {
            _current ??= ArrayPool<byte>.Shared.Rent(_blockSize);

            int count = Math.Min(data.Length, _blockSize - _currentLength);

            data[..count].CopyTo(_current.AsSpan(_currentLength));

            _currentLength += count;
            _appended += count;
            data = data[count..];

            if (_currentLength == _blockSize)
                SubmitCurrent();
        }
    }

    public void AppendZeros(long count)
    {
        if (count < 0 || _appended + count > _discSize)
            throw new InvalidDataException("GCZ에 디스크 크기보다 많은 데이터가 전달되었습니다.");

        while (count > 0)
        {
            if (_currentLength == 0 && count >= _blockSize)
            {
                _pipeline.Submit(_ => GetZeroBlock());

                _appended += _blockSize;
                count -= _blockSize;

                continue;
            }

            _current ??= ArrayPool<byte>.Shared.Rent(_blockSize);

            int part = (int)Math.Min(count, _blockSize - _currentLength);

            Array.Clear(_current, _currentLength, part);

            _currentLength += part;
            _appended += part;
            count -= part;

            if (_currentLength == _blockSize)
                SubmitCurrent();
        }
    }

    public void Finish()
    {
        if (_currentLength > 0)
        {
            Array.Clear(_current!, _currentLength, _blockSize - _currentLength);

            _currentLength = _blockSize;

            SubmitCurrent();
        }

        if (_appended != _discSize)
            throw new InvalidDataException("GCZ에 전달된 데이터가 디스크 크기보다 적습니다.");

        _pipeline.Drain();

        if (_completed != _blockCount)
            throw new InvalidDataException("GCZ 블록 수가 올바르지 않습니다.");

        byte[] header = new byte[_dataOffset];

        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), _subType);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8), (ulong)_dataPosition);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(16), (ulong)_discSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24), (uint)_blockSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28), (uint)_blockCount);

        for (int i = 0; i < _blockCount; i++)
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(HeaderSize + i * sizeof(ulong)), _pointers[i]);

        long hashOffset = HeaderSize + (long)_blockCount * sizeof(ulong);

        for (int i = 0; i < _blockCount; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan((int)hashOffset + i * sizeof(uint)), _hashes[i]);

        RandomAccess.Write(_output, header, 0);
    }

    private void SubmitCurrent()
    {
        byte[] block = _current!;

        _current = null;
        _currentLength = 0;

        if (block.AsSpan(0, _blockSize).IndexOfAnyExcept((byte)0) < 0)
        {
            ArrayPool<byte>.Shared.Return(block);
            _pipeline.Submit(_ => GetZeroBlock());
            return;
        }

        int blockSize = _blockSize;

        _pipeline.Submit(_ => CompressBlock(block, blockSize, true));
    }

    private BlockResult GetZeroBlock()
    {
        _zeroBlock ??= CompressBlock(new byte[_blockSize], _blockSize, false);

        return _zeroBlock.Value;
    }

    private void CompleteBlock(BlockResult result, int _)
    {
        int index = _completed++;

        _pointers[index] = (ulong)_dataPosition | (result.Stored ? UncompressedFlag : 0);
        _hashes[index] = result.Hash;

        RandomAccess.Write(_output, result.Buffer.AsSpan(0, result.Length), _dataOffset + _dataPosition);

        _dataPosition += result.Length;

        if (result.Rented != null)
            ArrayPool<byte>.Shared.Return(result.Rented);
    }

    private static BlockResult CompressBlock(byte[] block, int blockSize, bool pooled)
    {
        using var stream = new MemoryStream(blockSize);
        using (var zlib = new ZLibStream(stream, CompressionLevel.SmallestSize, true))
            zlib.Write(block, 0, blockSize);

        int compressedLength = (int)stream.Length;

        if (compressedLength > blockSize - 10)
        {
            uint storedHash = Adler32.Compute(block.AsSpan(0, blockSize));

            return new BlockResult(block, blockSize, true, storedHash, pooled ? block : null);
        }

        byte[] compressed = stream.GetBuffer();
        uint hash = Adler32.Compute(compressed.AsSpan(0, compressedLength));

        if (pooled)
            ArrayPool<byte>.Shared.Return(block);

        return new BlockResult(compressed, compressedLength, false, hash, null);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }
}