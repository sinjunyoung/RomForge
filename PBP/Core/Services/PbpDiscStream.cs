namespace PBP.Core.Services;

public class PbpDiscStream(PbpDiscEntry pbpDiscEntry) : Stream
{
    private readonly bool _dispose;
    private readonly byte[] _buffer = new byte[16 * PbpDiscEntry.ISO_BLOCK_SIZE];

    private int _bufPos;
    private int _bufLen;
    private long _position;
    private int _blockIndex;

    public PbpDiscStream(PbpDiscEntry pbpDiscEntry, bool dispose) : this(pbpDiscEntry)
    {
        _dispose = dispose;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= pbpDiscEntry.IsoSize)
            return 0;

        int totalRead = 0;

        while (count > 0)
        {
            if (_bufPos >= _bufLen)
            {
                _bufLen = (int)pbpDiscEntry.ReadBlock(_blockIndex++, _buffer);
                _bufPos = 0;

                if (_bufLen == 0)
                    break;
            }

            var available = _bufLen - _bufPos;
            var toCopy = Math.Min(available, count);

            if (_position + toCopy > pbpDiscEntry.IsoSize)
                toCopy = (int)(pbpDiscEntry.IsoSize - _position);

            Array.Copy(_buffer, _bufPos, buffer, offset, toCopy);

            _bufPos += toCopy;
            offset += toCopy;
            count -= toCopy;
            totalRead += toCopy;
            _position += toCopy;

            if (_position >= pbpDiscEntry.IsoSize) 
                break;
        }

        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => pbpDiscEntry.IsoSize + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(offset))
        };

        if (newPos < 0 || newPos > pbpDiscEntry.IsoSize)
            throw new IOException("Seek out of range");

        _position = newPos;
        _blockIndex = (int)(_position / _buffer.Length);
        _bufPos = _bufLen = 0;

        return _position;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => pbpDiscEntry.IsoSize;
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (_dispose) 
            pbpDiscEntry.Dispose();

        base.Dispose(disposing);
    }
}