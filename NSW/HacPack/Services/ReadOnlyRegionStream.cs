using System.IO;

namespace NSW.HacPack.Services;

public sealed class ReadOnlyRegionStream(Stream inner, long offset, long length) : Stream
{
    private long _position;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override int Read(byte[] buffer, int bufferOffset, int count)
    {
        long remaining = length - _position;

        if (remaining <= 0)
            return 0;

        int toRead = (int)Math.Min(count, remaining);

        inner.Position = offset + _position;

        int read = inner.Read(buffer, bufferOffset, toRead);

        _position += read;

        return read;
    }

    public override long Seek(long seekOffset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => seekOffset,
            SeekOrigin.Current => _position + seekOffset,
            _ => length + seekOffset
        };

        return _position;
    }

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int bufferOffset, int count) => throw new NotSupportedException();
}