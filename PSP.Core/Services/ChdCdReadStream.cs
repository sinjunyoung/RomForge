using CHD.Core.Interop;

namespace PSP.Core.Services;

public class ChdCdReadStream : Stream
{
    private const int PhysicalFrameSize = 2448;
    private const int LogicalSectorSize = 2048;

    private readonly LibChdrWrapper _wrapper;
    private readonly long _totalLength;
    private readonly int _skipBytes;
    private readonly uint _sectorsPerHunk;

    private long _position;
    private byte[]? _currentHunk;
    private uint _cachedHunkIndex = uint.MaxValue;

    public ChdCdReadStream(LibChdrWrapper wrapper, long totalLength, string? trackType = null)
    {
        _wrapper = wrapper;
        _totalLength = totalLength;
        _skipBytes = GetSkipBytes(trackType);
        _sectorsPerHunk = (wrapper.Header?.hunkbytes ?? 0) / (uint)PhysicalFrameSize;
    }

    private static int GetSkipBytes(string? trackType) => trackType?.ToUpperInvariant() switch
    {
        "MODE1" or "MODE1_RAW" => 16,
        "MODE2" or "MODE2_FORM1" or "MODE2_FORM2" or "MODE2_FORM_MIX" or "MODE2_RAW" => 24,
        _ => 16
    };

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;

        while (bytesRead < count && _position < _totalLength)
        {
            long sector = _position / LogicalSectorSize;
            int posInSector = (int)(_position % LogicalSectorSize);
            uint hunkIdx = (uint)(sector / _sectorsPerHunk);
            int frameOffset = (int)(sector % _sectorsPerHunk) * PhysicalFrameSize;

            if (_cachedHunkIndex != hunkIdx)
            {
                _currentHunk = _wrapper.ReadHunk(hunkIdx);
                _cachedHunkIndex = hunkIdx;
            }

            if (_currentHunk == null)
                throw new NullReferenceException(nameof(_currentHunk));

            int toRead = (int)Math.Min(count - bytesRead, LogicalSectorSize - posInSector);
            toRead = (int)Math.Min(toRead, _totalLength - _position);

            Array.Copy(_currentHunk, frameOffset + _skipBytes + posInSector, buffer, offset + bytesRead, toRead);

            bytesRead += toRead;
            _position += toRead;
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _totalLength + offset,
            _ => _position
        };
        _position = Math.Clamp(_position, 0, _totalLength);

        return _position;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _currentHunk = null;

        base.Dispose(disposing);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _totalLength;
    public override long Position { get => _position; set => _position = value; }
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}