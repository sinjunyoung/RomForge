namespace N3DS.Core.IO;

public class ConcatStream(params Stream[] streams) : Stream
{
    private int _current = 0;
    private long _position = 0;
    private readonly long _totalLength = streams.Sum(s => s.Length);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => _totalLength;

    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int totalRead = 0;
        int length = buffer.Length;

        while (totalRead < length && _current < streams.Length)
        {
            int read = streams[_current].Read(buffer[totalRead..]);

            if (read == 0)
            {
                _current++;
                continue;
            }

            totalRead += read;
            _position += read;
        }

        return totalRead;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        int totalRead = 0;
        int length = buffer.Length;

        while (totalRead < length && _current < streams.Length)
        {
            int read = await streams[_current].ReadAsync(buffer[totalRead..], ct).ConfigureAwait(false);

            if (read == 0)
            {
                _current++;
                continue;
            }

            totalRead += read;
            _position += read;
        }

        return totalRead;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (var s in streams)
                s.Dispose();

        base.Dispose(disposing);
    }
}