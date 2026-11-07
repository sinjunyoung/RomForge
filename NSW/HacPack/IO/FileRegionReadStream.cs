using System.IO;

namespace NSW.HacPack.IO;

public sealed class FileRegionReadStream(Stream inner, long baseOffset, long length, object sync) : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position { get; set; }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        long remaining = length - Position;

        if (remaining <= 0)
            return 0;

        int toRead = (int)Math.Min(buffer.Length, remaining);

        lock (sync)
        {
            inner.Position = baseOffset + Position;

            int read = inner.Read(buffer[..toRead]);

            Position += read;

            return read;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            _ => length + offset
        };

        return Position;
    }

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}