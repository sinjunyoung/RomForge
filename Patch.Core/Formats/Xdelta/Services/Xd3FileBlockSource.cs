using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

public sealed class Xd3FileBlockSource : IXd3BlockSource, IDisposable
{
    private const uint DefaultBlkSize = 16 * 1024 * 1024;

    private readonly FileStream _stream;
    private readonly bool _ownsStream;
    private byte[]? _reusableBuf;

    public Xd3FileBlockSource(string path, uint blkSize = DefaultBlkSize)
    {
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: false);
        _ownsStream = true;
        BlkSize = blkSize;
        FileLength = _stream.Length;
    }

    public Xd3FileBlockSource(FileStream stream, uint blkSize = DefaultBlkSize, bool ownsStream = false)
    {
        _stream = stream;
        _ownsStream = ownsStream;
        BlkSize = blkSize;
        FileLength = _stream.Length;
    }

    public uint BlkSize { get; }
    public long FileLength { get; }

    public Xd3Source CreateSource()
    {
        var source = new Xd3Source
        {
            BlkSize = BlkSize,
            MaxWinSize = Math.Max(FileLength, 1)
        };

        Xd3SourceOps.SetSourceAndSize(source, FileLength);

        return source;
    }

    public void FillBlock(Xd3Source source, long blockNumber)
    {
        long offset = blockNumber * BlkSize;

        if (offset < 0 || offset > FileLength)
            throw new Xd3Exception("block number out of range");

        int toRead = (int)Math.Min(BlkSize, FileLength - offset);

        if (_reusableBuf == null || _reusableBuf.Length != toRead)
            _reusableBuf = new byte[toRead];

        _stream.Seek(offset, SeekOrigin.Begin);

        int totalRead = 0;

        while (totalRead < toRead)
        {
            int n = _stream.Read(_reusableBuf, totalRead, toRead - totalRead);

            if (n == 0)
                throw new Xd3Exception("unexpected end of source file");

            totalRead += n;
        }

        source.CurBlk = _reusableBuf;
        source.OnBlk = (uint)toRead;
        source.CurBlkNo = blockNumber;
    }

    public void Dispose()
    {
        if (_ownsStream)
            _stream.Dispose();
    }
}