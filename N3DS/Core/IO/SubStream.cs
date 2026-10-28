namespace N3DS.Core.IO;

public class SubStream(Stream baseStream, long start, long length) : Stream
{
    public override long Length => length;

    public override long Position { get; set; }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int count = (int)Math.Min(buffer.Length, length - Position);

        if (count <= 0)
            return 0;

        lock (baseStream) { baseStream.Position = start + Position; }
        int read = baseStream.Read(buffer[..count]);

        Position += read;

        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        int count = (int)Math.Min(buffer.Length, length - Position);

        if (count <= 0)
            return 0;

        Memory<byte> slicedBuffer = buffer[..count];

        if (baseStream is FileStream fs)
        {
            int read = await RandomAccess.ReadAsync(fs.SafeFileHandle, slicedBuffer, start + Position, ct).ConfigureAwait(false);
            Position += read;
            return read;
        }

        lock (baseStream) { baseStream.Position = start + Position; }
        int r = await baseStream.ReadAsync(slicedBuffer, ct).ConfigureAwait(false);

        Position += r;

        return r;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => length + offset,
            _ => Position,
        };

        return Position;
    }

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}