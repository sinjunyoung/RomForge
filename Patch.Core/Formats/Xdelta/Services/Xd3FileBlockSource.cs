using Patch.Core.Formats.Xdelta.Models;
using System.IO.MemoryMappedFiles;

namespace Patch.Core.Formats.Xdelta.Services;

public sealed class Xd3FileBlockSource : IXd3BlockSource, IDisposable
{
    private const uint DefaultBlkSize = 256 * 1024;

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private byte[]? _reusableBuf;

    public Xd3FileBlockSource(string path, uint blkSize = DefaultBlkSize)
    {
        FileLength = new FileInfo(path).Length;
        BlkSize = blkSize;

        _mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, mapName: null, capacity: 0, MemoryMappedFileAccess.Read);
        _accessor = _mmf.CreateViewAccessor(0, FileLength, MemoryMappedFileAccess.Read);
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
        {
            throw new Xd3Exception("block number out of range");
        }

        int toRead = (int)Math.Min(BlkSize, FileLength - offset);

        if (_reusableBuf == null || _reusableBuf.Length != toRead)
        {
            _reusableBuf = new byte[toRead];
        }

        _accessor.ReadArray(offset, _reusableBuf, 0, toRead);

        source.CurBlk = _reusableBuf;
        source.OnBlk = (uint)toRead;
        source.CurBlkNo = blockNumber;
    }

    public void Dispose()
    {
        _accessor.Dispose();
        _mmf.Dispose();
    }
}