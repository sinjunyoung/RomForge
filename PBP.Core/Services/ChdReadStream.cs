using CHD.Core.Interop;
using CHD.Core.Models;

namespace PBP.Core.Services;

public class ChdReadStream(LibChdrWrapper wrapper, long totalLength, ChdInfo info) : Stream
{
    private long _position;
    private byte[]? _currentHunk;
    private uint _cachedHunkIndex = uint.MaxValue;
    private readonly uint _sectorsPerHunk = (wrapper.Header?.hunkbytes ?? 0) / 2448u;
    private readonly TrackRegion[] _tracks = BuildTrackRegions(info);

    private record TrackRegion(long StartSector, long EndSector, bool IsAudio);

    private static TrackRegion[] BuildTrackRegions(ChdInfo info)
    {
        var regions = new TrackRegion[info.Tracks.Length];
        long current = 0;

        for (int i = 0; i < info.Tracks.Length; i++)
        {
            var track = info.Tracks[i];
            bool isAudio = track.TrackType?.ToUpperInvariant().Contains("AUDIO") == true;
            regions[i] = new TrackRegion(current, current + track.Frames, isAudio);
            current += track.Frames;
        }

        return regions;
    }

    private bool IsAudioSector(long sectorIdx)
    {
        foreach (var t in _tracks)
        {
            if (sectorIdx < t.EndSector)
                return t.IsAudio;
        }
        return false;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;

        while (bytesRead < count && _position < totalLength)
        {
            uint sectorIdx = (uint)(_position / 2352);
            int posInSector = (int)(_position % 2352);

            uint hunkIdx = (uint)(sectorIdx / _sectorsPerHunk);
            int hunkOffset = (int)(sectorIdx % _sectorsPerHunk) * 2448;

            if (_cachedHunkIndex != hunkIdx)
            {
                _currentHunk = wrapper.ReadHunk(hunkIdx);
                _cachedHunkIndex = hunkIdx;
            }

            if (_currentHunk == null)
                throw new NullReferenceException(nameof(_currentHunk));

            if (IsAudioSector(sectorIdx) && posInSector == 0)
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

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => totalLength;
    public override long Position { get => _position; set => _position = value; }
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}