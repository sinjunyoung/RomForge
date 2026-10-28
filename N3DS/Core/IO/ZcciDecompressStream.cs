using N3DS.Core.Models;
using N3DS.Core.Services;
using System.Buffers;
using ZstdSharp;

namespace N3DS.Core.IO;

public class ZcciDecompressStream : Stream
{
    private readonly Stream _base;
    private readonly List<(long compOffset, int compSize, int decompSize)> _blocks;
    private readonly long _totalSize;

    private int _cachedBlockIndex = -1;
    private byte[]? _cachedBlock;

    public ZcciDecompressStream(Stream baseStream, Z3dsHeader header)
    {
        _base = baseStream;
        _totalSize = header.UncompressedSize;

        long compressedDataOffset = header.HeaderSize + header.MetadataSize;
        long compressedDataLength = header.CompressedSize;
        var entries = Z3dsFormat.ParseSeekTable(_base, compressedDataOffset, compressedDataLength);

        _blocks = new List<(long, int, int)>(entries.Count);

        long offset = compressedDataOffset;

        foreach (var e in entries)
        {
            _blocks.Add((offset, (int)e.CompressedSize, (int)e.DecompressedSize));

            offset += e.CompressedSize;
        }
    }

    public override long Length => _totalSize;

    public override long Position { get; set; }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Position >= _totalSize) 
            return 0;

        count = (int)Math.Min(count, _totalSize - Position);

        int totalRead = 0;

        while (totalRead < count)
        {
            long decompPos = 0;
            int blockIndex = 0;

            for (int i = 0; i < _blocks.Count; i++)
            {
                if (Position < decompPos + _blocks[i].decompSize)
                {
                    blockIndex = i;
                    break;
                }

                decompPos += _blocks[i].decompSize;
                blockIndex = i + 1;
            }

            if (blockIndex >= _blocks.Count)
                break;

            if (_cachedBlockIndex != blockIndex)
            {
                var (compOffset, compSize, decompSize) = _blocks[blockIndex];
                byte[] compBuf = ArrayPool<byte>.Shared.Rent(compSize);
                byte[] decompBuf = new byte[decompSize];

                try
                {
                    _base.Position = compOffset;
                    _base.ReadExactly(compBuf, 0, compSize);

                    using var decompressor = new Decompressor();

                    decompressor.Unwrap(compBuf.AsSpan(0, compSize), decompBuf.AsSpan(0, decompSize));
                }
                finally 
                { 
                    ArrayPool<byte>.Shared.Return(compBuf); 
                }

                _cachedBlock = decompBuf;
                _cachedBlockIndex = blockIndex;
            }

            long blockDecompStart = 0;

            for (int i = 0; i < blockIndex; i++)
                blockDecompStart += _blocks[i].decompSize;

            int blockOffset = (int)(Position - blockDecompStart);
            int canRead = Math.Min(count - totalRead, _cachedBlock!.Length - blockOffset);

            Buffer.BlockCopy(_cachedBlock, blockOffset, buffer, offset + totalRead, canRead);

            totalRead += canRead;
            Position += canRead;
        }

        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => _totalSize + offset,
            _ => Position
        };
        return Position;
    }

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) 
            _base.Dispose();

        base.Dispose(disposing);
    }
}