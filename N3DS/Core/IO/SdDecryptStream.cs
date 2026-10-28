using N3DS.Core.Crypto;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace N3DS.Core.IO;

public class SdDecryptStream(Stream baseStream, string sdPath, SdCrypto sdCrypto, bool leaveOpen = false) : Stream
{
    private readonly byte[] _key = sdCrypto.GetKey();
    private readonly byte[] _iv = SdCrypto.PathToIv(sdPath);

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        long pos = Position;

        baseStream.Position = pos;

        int bytesRead = baseStream.Read(buffer);

        if (bytesRead <= 0)
            return bytesRead;

        DecryptBlock(buffer, pos, bytesRead);

        Position += bytesRead;

        return bytesRead;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        long pos = Position;

        baseStream.Position = pos;

        int bytesRead = await baseStream.ReadAsync(buffer, ct).ConfigureAwait(false);

        if (bytesRead <= 0)
            return bytesRead;

        DecryptBlock(buffer.Span, pos, bytesRead);

        Position += bytesRead;

        return bytesRead;
    }

    private void DecryptBlock(Span<byte> buffer, long streamPos, int length)
    {
        using var aes = Aes.Create();

        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = _key;

        using var enc = aes.CreateEncryptor();
        byte[] ctr = (byte[])_iv.Clone();
        long blocks = streamPos / 16;

        SeekCtr(ctr, blocks);

        byte[] keystream = new byte[16];
        byte[] ctrBuf = new byte[16];
        int blockOffset = (int)(streamPos % 16);
        int pos = 0;

        while (pos < length)
        {
            Buffer.BlockCopy(ctr, 0, ctrBuf, 0, 16);
            enc.TransformBlock(ctrBuf, 0, 16, keystream, 0);

            int chunk = Math.Min(16 - blockOffset, length - pos);

            for (int i = 0; i < chunk; i++)
                buffer[pos + i] ^= keystream[blockOffset + i];

            if (blockOffset + chunk >= 16)
                IncrementCtr(ctr);

            pos += chunk;
            blockOffset = 0;
        }
    }

    private static void SeekCtr(byte[] ctr, long blocks)
    {
        if (blocks == 0)
            return;

        ulong hi = BinaryPrimitives.ReadUInt64BigEndian(ctr.AsSpan(0));
        ulong lo = BinaryPrimitives.ReadUInt64BigEndian(ctr.AsSpan(8));
        ulong oldLo = lo;

        lo += (ulong)blocks;

        if (lo < oldLo)
            hi++;

        BinaryPrimitives.WriteUInt64BigEndian(ctr.AsSpan(0), hi);
        BinaryPrimitives.WriteUInt64BigEndian(ctr.AsSpan(8), lo);
    }

    private static void IncrementCtr(byte[] ctr)
    {
        for (int i = 15; i >= 0; i--)
            if (++ctr[i] != 0)
                break;
    }

    public override long Position { get; set; }

    public override long Length => baseStream.Length;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => Position
        };
        return Position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
            baseStream.Dispose();

        base.Dispose(disposing);
    }
}