using CHD.Core.Interop;
using CHD.Core.Models;

namespace PBP.Core.Services;

public class ChdReadStream(LibChdrWrapper wrapper, long totalLength, ChdInfo info) : Stream
{
    private long _position;
    private byte[]? _currentHunk;
    private uint _cachedHunkIndex = uint.MaxValue;
    private readonly uint _sectorsPerHunk = (wrapper.Header?.hunkbytes ?? 0) / 2448u;
    private readonly TrackInfo[] _tracks = info.Tracks;

    private (long chdsector, int tracknum) PhysicalToChdLba(long physlba)
    {
        for (int i = 0; i < _tracks.Length - 1; i++)
        {
            if (physlba < _tracks[i + 1].PhysFrameOfs)
            {
                long chdsector = physlba - _tracks[i].PhysFrameOfs + _tracks[i].ChdFrameOfs;
                return (chdsector, i);
            }
        }
        return (physlba, 0);
    }

    private bool IsAudio(int tracknum) =>
        _tracks[tracknum].TrackType?.ToUpperInvariant().Contains("AUDIO", StringComparison.InvariantCultureIgnoreCase) == true;

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;

        while (bytesRead < count && _position < totalLength)
        {
            long physlba = _position / 2352;
            int posInSector = (int)(_position % 2352);

            var (chdsector, tracknum) = PhysicalToChdLba(physlba);

            uint hunkIdx = (uint)(chdsector / _sectorsPerHunk);
            int hunkOffset = (int)(chdsector % _sectorsPerHunk) * 2448;

            if (_cachedHunkIndex != hunkIdx)
            {
                _currentHunk = wrapper.ReadHunk(hunkIdx);
                _cachedHunkIndex = hunkIdx;
            }

            if (_currentHunk == null)
                throw new NullReferenceException(nameof(_currentHunk));

            if (IsAudio(tracknum) && posInSector == 0)
            {
                for (int i = hunkOffset; i < hunkOffset + 2352; i += 2)
                    (_currentHunk[i], _currentHunk[i + 1]) = (_currentHunk[i + 1], _currentHunk[i]);
            }

            int toRead = (int)Math.Min(count - bytesRead, 2352 - posInSector);
            toRead = (int)Math.Min(toRead, totalLength - _position);

            Array.Copy(_currentHunk, hunkOffset + posInSector, buffer, offset + bytesRead, toRead);
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
            SeekOrigin.End => totalLength + offset,
            _ => _position
        };
        _position = Math.Clamp(_position, 0, totalLength);
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
    public override long Length => totalLength;
    public override long Position { get => _position; set => _position = value; }
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}