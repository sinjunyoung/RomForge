namespace PBP.Core.Services;

public sealed class ConcatenatedStream(IReadOnlyList<Stream> streams) : Stream
{
    private int _currentIndex = 0;
    private long _position = 0;
    private readonly long _totalLength = streams.Sum(s => s.Length);

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;

        while (bytesRead < count && _currentIndex < streams.Count)
        {
            int read = streams[_currentIndex].Read(buffer, offset + bytesRead, count - bytesRead);

            if (read == 0)
            {
                _currentIndex++;
                continue;
            }

            bytesRead += read;
            _position += read;
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _totalLength + offset,
            _ => _position
        };

        target = Math.Clamp(target, 0, _totalLength);

        long accumulatedLength = 0;
        _currentIndex = streams.Count;
        _position = target;

        for (int i = 0; i < streams.Count; i++)
        {
            var s = streams[i];

            if (accumulatedLength + s.Length > target)
            {
                if (_currentIndex == streams.Count)
                    _currentIndex = i;

                s.Seek(target - accumulatedLength, SeekOrigin.Begin);
            }
            else
                s.Seek(s.Length, SeekOrigin.Begin);

            if (accumulatedLength > target)
                s.Seek(0, SeekOrigin.Begin);

            accumulatedLength += s.Length;
        }

        return _position;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _totalLength;
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
    public override void Flush() { }
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