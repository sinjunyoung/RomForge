namespace N3DS.Core.IO;

public class SubStream(Stream baseStream, long start, long length) : Stream
{
    private long _position;

    public override long Length => length;
    public override long Position { get => _position; set => _position = value; }
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;

    public override int Read(byte[] buffer, int offset, int count)
    {
        count = (int)Math.Min(count, length - _position);

        if (count <= 0)
            return 0;

        lock (baseStream) { baseStream.Position = start + _position; }
        int read = baseStream.Read(buffer, offset, count);

        _position += read;

        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        count = (int)Math.Min(count, length - _position);

        if (count <= 0)
            return 0;

        if (baseStream is FileStream fs)
        {
            int read = await RandomAccess.ReadAsync(fs.SafeFileHandle, buffer.AsMemory(offset, count), start + _position, ct);
            _position += read;

            return read;
        }

        lock (baseStream) { baseStream.Position = start + _position; }
        int r = await baseStream.ReadAsync(buffer.AsMemory(offset, count), ct);

        _position += r;

        return r;
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