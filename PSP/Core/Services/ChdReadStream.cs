using CHD.Core.Interop;

namespace PSP.Core.Services;

public class ChdReadStream(LibChdrWrapper wrapper, long totalLength) : Stream
{
    private byte[]? _currentHunk;
    private uint _cachedHunkIndex = uint.MaxValue;
    private readonly uint _sectorsPerHunk = (wrapper.Header?.hunkbytes ?? 0) / 2048u;

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;

        while (bytesRead < count && Position < totalLength)
        {
            long sector = Position / 2048;
            int posInSector = (int)(Position % 2048);
            uint hunkIdx = (uint)(sector / _sectorsPerHunk);
            int hunkOffset = (int)(sector % _sectorsPerHunk) * 2048;

            if (_cachedHunkIndex != hunkIdx)
            {
                _currentHunk = wrapper.ReadHunk(hunkIdx);
                _cachedHunkIndex = hunkIdx;
            }

            if (_currentHunk == null)
                throw new NullReferenceException(nameof(_currentHunk));

            int toRead = (int)Math.Min(count - bytesRead, 2048 - posInSector);

            toRead = (int)Math.Min(toRead, totalLength - Position);

            Array.Copy(_currentHunk, hunkOffset + posInSector, buffer, offset + bytesRead, toRead);

            bytesRead += toRead;
            Position += toRead;
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => totalLength + offset,
            _ => Position
        };

        Position = Math.Clamp(Position, 0, totalLength);

        return Position;
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

    public override long Length => totalLength;

    public override long Position { get; set; }

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}