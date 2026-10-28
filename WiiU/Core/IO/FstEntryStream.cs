using WiiU.Core.Models;
using WiiU.Core.Services;

namespace WiiU.Core.IO;

internal sealed class FstEntryStream(WupTitleSource owner, FstEntry entry, string path) : Stream
{
    private long _position;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => entry.FileSize;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remaining = entry.FileSize - _position;
        if (remaining <= 0)
            return 0;

        int toRead = (int)Math.Min(count, remaining);

        try
        {
            owner.ReadFileEntry(entry, _position, buffer, offset, toRead);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"'{path}' 읽기 실패 (clusterIndex={entry.ClusterIndex}, offsetField={entry.FileOffsetField}, " + $"byteOffset={(long)entry.FileOffsetField * owner.FstOffsetFactor}, size={entry.FileSize}): {ex.Message}", ex);
        }

        _position += toRead;
        return toRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => entry.FileSize + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        return _position;
    }

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}