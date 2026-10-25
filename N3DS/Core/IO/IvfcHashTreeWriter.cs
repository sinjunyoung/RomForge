using System.Buffers.Binary;
using System.Security.Cryptography;
using N3DS.Core.Services;

namespace N3DS.Core.IO;

public static class IvfcHashTreeWriter
{
    private const int Sha256Len = 0x20;

    public static void WriteIvfcLevel(byte[] buf, int offset, ulong logicalOffset, ulong size)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(offset + 0x00), logicalOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(offset + 0x08), size);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset + 0x10), 12);
    }

    public static async Task GenHashLevelAsync(Stream stream, long dataOffset, long hashOffset, ulong dataSize, int blockSize, CancellationToken ct)
    {
        ulong numBlocks = RomFsFormat.AlignUp(dataSize, (ulong)blockSize) / (ulong)blockSize;
        byte[] block = new byte[blockSize];

        for (ulong i = 0; i < numBlocks; i++)
        {
            stream.Position = dataOffset + (long)(i * (ulong)blockSize);

            ulong remaining = dataSize - i * (ulong)blockSize;
            int copySize = (int)Math.Min((ulong)blockSize, remaining);

            Array.Clear(block);
            await stream.ReadExactlyAsync(block.AsMemory(0, copySize), ct);

            byte[] hash = SHA256.HashData(block);

            stream.Position = hashOffset + (long)(i * Sha256Len);

            await stream.WriteAsync(hash, ct);
        }
    }

    public sealed class Level3HashingStream(Stream inner, int blockSize) : Stream
    {
        private readonly byte[] _block = new byte[blockSize];
        private readonly List<byte[]> _hashes = [];
        private int _blockPos;

        public byte[] GetHashResult()
        {
            if (_blockPos > 0)
            {
                Array.Clear(_block, _blockPos, blockSize - _blockPos);
                _hashes.Add(SHA256.HashData(_block));
            }

            byte[] result = new byte[_hashes.Count * Sha256Len];

            for (int i = 0; i < _hashes.Count; i++)
                _hashes[i].CopyTo(result, i * Sha256Len);

            return result;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            await inner.WriteAsync(buffer, ct);

            int offset = 0;

            while (offset < buffer.Length)
            {
                int toCopy = Math.Min(blockSize - _blockPos, buffer.Length - offset);

                buffer.Slice(offset, toCopy).CopyTo(_block.AsMemory(_blockPos));

                _blockPos += toCopy;
                offset += toCopy;

                if (_blockPos == blockSize)
                {
                    _hashes.Add(SHA256.HashData(_block));

                    _blockPos = 0;
                }
            }
        }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }
}