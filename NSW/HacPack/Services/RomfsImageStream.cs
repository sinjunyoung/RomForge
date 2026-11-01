using NSW.HacPack.Models;
using System.IO;

namespace NSW.HacPack.Services;

internal sealed class RomfsImageStream : Stream, IUnpaddedLengthSource
{
    private const long FilePartitionOfs = 0x200;
    private const long BlockSize = IvfcConstants.HashBlockSize;

    private readonly RomfsLayout _layout;
    private readonly byte[] _headerBlock;
    private readonly byte[] _tables;
    private readonly long _dataEnd;
    private readonly long _tablesStart;
    private readonly long _length;

    private long _position;
    private int _fileIndex;
    private Stream? _currentFile;

    public RomfsImageStream(RomfsLayout layout)
    {
        _layout = layout;

        var header = layout.Header;

        _headerBlock = new byte[FilePartitionOfs];

        NcaStructHelper.StructToBytes(ref header).CopyTo(_headerBlock, 0);

        _tables = [.. layout.DirHashBytes, .. layout.DirTableBuf, .. layout.FileHashBytes, .. layout.FileTableBuf];
        _dataEnd = FilePartitionOfs + (long)layout.FilePartitionSize;
        _tablesStart = (long)layout.Header.DirHashTableOfs;

        long end = _tablesStart + _tables.Length;

        _length = (end + BlockSize - 1) / BlockSize * BlockSize;
    }

    public long UnpaddedLength => _tablesStart + _tables.Length;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set
        {
            if (value != _position)
                throw new NotSupportedException();
        }
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int total = 0;

        while (total < buffer.Length && _position < _length)
        {
            int read = ReadSegment(buffer[total..]);

            if (read <= 0)
                break;

            total += read;
            _position += read;
        }

        return total;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _currentFile?.Dispose();
            _currentFile = null;
        }

        base.Dispose(disposing);
    }

    private int ReadSegment(Span<byte> buffer)
    {
        if (_position < FilePartitionOfs)
            return CopyFrom(_headerBlock, (int)_position, buffer);

        if (_position < _dataEnd)
            return ReadData(buffer);

        if (_position < _tablesStart)
            return Zero(buffer, _tablesStart - _position);

        long tableOffset = _position - _tablesStart;

        if (tableOffset < _tables.Length)
            return CopyFrom(_tables, (int)tableOffset, buffer);

        return Zero(buffer, _length - _position);
    }

    private int ReadData(Span<byte> buffer)
    {
        while (_fileIndex < _layout.Files.Count)
        {
            var file = _layout.Files[_fileIndex];
            long start = FilePartitionOfs + (long)file.Offset;
            long end = start + (long)file.Size;

            if (_position >= end)
            {
                _currentFile?.Dispose();
                _currentFile = null;
                _fileIndex++;

                continue;
            }

            if (_position < start)
                return Zero(buffer, start - _position);

            _currentFile ??= file.Open!();

            int toRead = (int)Math.Min(buffer.Length, end - _position);
            int read = _currentFile.Read(buffer[..toRead]);

            if (read <= 0)
                throw new InvalidDataException($"romfs 파일 데이터가 예상보다 짧습니다: {file.SumPath}");

            return read;
        }

        return Zero(buffer, _dataEnd - _position);
    }

    private static int CopyFrom(byte[] source, int offset, Span<byte> buffer)
    {
        int count = Math.Min(buffer.Length, source.Length - offset);

        source.AsSpan(offset, count).CopyTo(buffer);

        return count;
    }

    private static int Zero(Span<byte> buffer, long available)
    {
        int count = (int)Math.Min(buffer.Length, available);

        buffer[..count].Clear();

        return count;
    }
}