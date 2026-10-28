using System.IO;

namespace NSW.HacPack.Services;

public sealed class OffsetStream(Stream inner, long baseOffset, Action<long>? onAdvance = null) : Stream
{
    private long _position;
    private long _highWater;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => true;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => inner.Length - baseOffset;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        inner.Position = baseOffset + _position;

        int read = inner.Read(buffer, offset, count);

        _position += read;

        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        inner.Position = baseOffset + _position;

        int read = inner.Read(buffer);

        _position += read;

        return read;
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        inner.Position = baseOffset + _position;

        inner.Write(buffer);

        _position += buffer.Length;

        if (_position > _highWater)
        {
            long delta = _position - _highWater;

            _highWater = _position;

            onAdvance?.Invoke(delta);
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => Length + offset
        };

        return _position;
    }

    public override void Flush() => inner.Flush();

    public override void SetLength(long value) => throw new NotSupportedException();
}