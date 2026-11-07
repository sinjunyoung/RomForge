using System.IO;

namespace NSW.HacPack.IO;

public sealed class FileRegionWriteStream(Stream inner, long baseOffset, object sync) : Stream
{
    private long _length;

    public override bool CanRead => false;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => _length;

    public override long Position { get; set; }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        lock (sync)
        {
            inner.Position = baseOffset + Position;

            inner.Write(buffer);
        }

        Position += buffer.Length;

        if (Position > _length)
            _length = Position;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);

        return ValueTask.CompletedTask;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            _ => _length + offset
        };

        return Position;
    }

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}