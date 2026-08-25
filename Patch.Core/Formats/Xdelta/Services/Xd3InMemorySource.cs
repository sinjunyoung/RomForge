using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

public sealed class Xd3InMemorySource(byte[] data) : IXd3BlockSource
{
    public static Xd3Source CreateSource(byte[] data)
    {
        var source = new Xd3Source
        {
            BlkSize = (uint)Math.Max(data.Length, 1),
            MaxWinSize = Math.Max(data.Length, 1)
        };

        Xd3SourceOps.SetSourceAndSize(source, data.Length);

        return source;
    }

    public void FillBlock(Xd3Source source, long blockNumber)
    {
        if (blockNumber != 0)
            throw new Xd3Exception("block number out of range for in-memory source");

        source.CurBlk = data;
        source.OnBlk = (uint)data.Length;
        source.CurBlkNo = 0;
    }
}