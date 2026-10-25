using System.Security.Cryptography;

namespace N3DS.Core.IO;

public class AesCbcSubStream(Stream baseStream, long start, long length, byte[] key, byte[] iv) : Stream
{
    private readonly byte[] _initialIv = (byte[])iv.Clone();
    private long _position;
    private readonly byte[] _currentIv = (byte[])iv.Clone();
    private const int BlockSize = 16;

    public override long Length => length;
    public override long Position
    {
        get => _position;
        set => _position = value;
    }
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;

    public override int Read(byte[] buffer, int offset, int count)
    {
        count = (int)Math.Min(count, length - _position);

        if (count <= 0)
            return 0;

        long blockStart = (_position / 16) * 16;
        int prefixBytes = (int)(_position - blockStart);
        int aligned = ((prefixBytes + count + 15) / 16) * 16;
        byte[] encrypted = new byte[aligned];
        lock (baseStream) { baseStream.Position = start + blockStart; }
        int read = baseStream.Read(encrypted, 0, aligned);

        read = (read / 16) * 16;

        if (read == 0) 
            return 0;

        byte[] iv;

        if (blockStart == 0)
            iv = (byte[])_initialIv.Clone();
        else
        {
            iv = new byte[16];

            lock (baseStream)             
                baseStream.Position = start + blockStart - 16;

            baseStream.ReadExactly(iv, 0, 16);
        }

        using var aes = Aes.Create();

        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        byte[] decrypted = aes.DecryptCbc(encrypted.AsSpan(0, read), iv, PaddingMode.None);
        int copyLen = Math.Min(count, read - prefixBytes);

        decrypted.AsSpan(prefixBytes, copyLen).CopyTo(buffer.AsSpan(offset));

        _position += copyLen;

        return copyLen;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => Task.FromResult(Read(buffer, offset, count));

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        byte[] tmp = new byte[buffer.Length];
        int read = Read(tmp, 0, tmp.Length);

        tmp.AsMemory(0, read).CopyTo(buffer);

        return ValueTask.FromResult(read);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => length + offset,
            _ => _position,
        };

        return _position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}