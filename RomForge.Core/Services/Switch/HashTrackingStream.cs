using System.IO;
using System.Security.Cryptography;

namespace RomForge.Core.Services.Switch;

public sealed class HashTrackingStream(Stream inner, long hashLimit) : Stream
{
    private readonly IncrementalHash _sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    private long _bytesHashed;

    public byte[] GetHash() => _sha256.GetHashAndReset();

    public override bool CanRead => false;

    public override bool CanSeek => inner.CanSeek;

    public override bool CanWrite => true;

    public override long Length => inner.Length;

    public override long Position { get => inner.Position; set => inner.Position = value; }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void SetLength(long value) => inner.SetLength(value);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        HashChunk(buffer.AsSpan(offset, count));
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        await inner.WriteAsync(buffer.AsMemory(offset, count), ct);
        HashChunk(buffer.AsSpan(offset, count));
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        await inner.WriteAsync(buffer, ct);
        HashChunk(buffer.Span);
    }

    private void HashChunk(ReadOnlySpan<byte> data)
    {
        if (_bytesHashed >= hashLimit)
            return;

        long remaining = hashLimit - _bytesHashed;
        var chunk = data.Length <= remaining ? data : data[..(int)remaining];

        _sha256.AppendData(chunk);
        _bytesHashed += chunk.Length;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _sha256.Dispose();
    }
}