using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using CHD.Core.Services;

namespace PBP.Core.Services;

public static class RawDiscProcessor
{
    public static ResolvedDisc Resolve(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();

        if (ext == ".cue")
        {
            return CuePreprocessor.Resolve(filePath);
        }
        else if (ext == ".chd")
        {
            var wrapper = new LibChdrWrapper();
            var error = wrapper.Open(filePath);
            if (error != ChdrError.CHDERR_NONE)
                throw new Exception($"CHD 열기 실패: {LibChdrWrapper.GetErrorString(error)}");

            var info = ChdInfoReader.ReadChdInfo(filePath);
            long totalSize = ChdmanService.CalculateOriginalSize(info);

            var stream = new ChdReadStream(wrapper, totalSize);

            string cueContent = ChdmanService.GenerateCueContent(filePath);
            var cueFile = CueFileReader.Parse(cueContent);
            byte[] tocData = TocBuilder.BuildToc(cueFile, (uint)totalSize);

            return ResolvedDisc.Create(stream, stream.Length, tocData);
        }

        throw new NotSupportedException($"지원하지 않는 확장자: {ext}");
    }
}

public class ChdReadStream(LibChdrWrapper wrapper, long totalLength) : Stream
{
    private long _position;
    private byte[] _currentHunk;
    private uint _cachedHunkIndex = uint.MaxValue;
    private readonly uint _hunkBytes = wrapper.Header?.hunkbytes ?? 0;

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;
        while (bytesRead < count && _position < totalLength)
        {
            uint hunkIdx = (uint)(_position / _hunkBytes);
            int posInHunk = (int)(_position % _hunkBytes);

            if (_cachedHunkIndex != hunkIdx)
            {
                _currentHunk = wrapper.ReadHunk(hunkIdx);
                _cachedHunkIndex = hunkIdx;
            }

            int toRead = (int)Math.Min(count - bytesRead, _hunkBytes - posInHunk);
            toRead = (int)Math.Min(toRead, totalLength - _position);

            Array.Copy(_currentHunk, posInHunk, buffer, offset + bytesRead, toRead);

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