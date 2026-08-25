namespace Patch.Core.Formats.Xdelta.Models;

public sealed class Xd3Source
{
    public uint BlkSize;
    public string? Name;
    public long MaxWinSize;

    public long CurBlkNo;
    public uint OnBlk;
    public byte[]? CurBlk;

    public uint SrcLen;
    public long SrcBase;
    public uint ShiftBy;
    public uint MaskBy;
    public long CpyOffBlocks;
    public uint CpyOffBlkOff;
    public long GetBlkNo;

    public long MaxBlkNo;
    public uint OnLastBlk;
    public bool EofKnown;
}