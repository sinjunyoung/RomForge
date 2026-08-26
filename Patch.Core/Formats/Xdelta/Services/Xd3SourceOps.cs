using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

public static class Xd3SourceOps
{
    public static bool CheckPow2(long value, out uint log)
    {
        long x = 1;
        log = 0;

        while (x != 0)
        {
            if (x == value)
                return true;

            x <<= 1;
            log += 1;
        }

        return false;
    }

    public static uint Pow2Roundup(uint x)
    {
        uint i = 1;

        while (x > i)
            i <<= 1;

        return i;
    }

    public static long XoffRoundup(long x)
    {
        long i = 1;

        while (x > i)
            i <<= 1;

        return i;
    }

    public static void BlksizeDiv(long offset, Xd3Source source, out long blkno, out uint blkoff)
    {
        blkno = offset >> (int)source.ShiftBy;
        blkoff = (uint)(offset & source.MaskBy);
    }

    public static void BlksizeAdd(ref long blkno, ref uint blkoff, Xd3Source source, uint add)
    {
        blkoff += add;

        uint blkdiff = blkoff >> (int)source.ShiftBy;

        if (blkdiff != 0)
        {
            blkno += blkdiff;
            blkoff &= source.MaskBy;
        }
    }

    public static void SetSource(Xd3Source src)
    {
        src.SrcLen = 0;
        src.SrcBase = 0;

        if (!CheckPow2(src.BlkSize, out uint shiftby))
        {
            src.BlkSize = Pow2Roundup(src.BlkSize);
            CheckPow2(src.BlkSize, out shiftby);
        }

        src.ShiftBy = shiftby;
        src.MaskBy = (1U << (int)shiftby) - 1U;

        if (!CheckPow2(src.MaxWinSize, out _))
            src.MaxWinSize = XoffRoundup(src.MaxWinSize);

        src.MaxWinSize = Math.Max(src.MaxWinSize, Xd3Constants.AllocSize);

        if (src.MaxWinSize > Xd3Constants.MaxSrcWinSz)
            throw new Xd3Exception("source max_winsize exceeds the maximum");
    }

    public static void SetSourceAndSize(Xd3Source src, long sourceSize)
    {
        SetSource(src);

        src.EofKnown = true;

        BlksizeDiv(sourceSize, src, out long maxBlkNo, out uint onLastBlk);

        src.MaxBlkNo = maxBlkNo;
        src.OnLastBlk = onLastBlk;
    }

    public static long SourceEof(Xd3Source src) => (src.MaxBlkNo << (int)src.ShiftBy) + src.OnLastBlk;

    public static uint BytesOnSrcBlk(Xd3Source src, long blkno) => blkno == src.MaxBlkNo ? src.OnLastBlk : src.BlkSize;

    public static void GetBlk(Xd3Source src, IXd3BlockSource? blockSource, long blkno)
    {
        if (src.CurBlk == null || blkno != src.CurBlkNo)
        {
            src.GetBlkNo = blkno;

            if (blockSource == null)
                throw new Xd3Exception("getblk source input");

            blockSource.FillBlock(src, blkno);
        }

        if (blkno > src.MaxBlkNo)
        {
            src.MaxBlkNo = blkno;

            if (src.OnBlk != src.BlkSize)
                src.EofKnown = true;
        }

        if (blkno == src.MaxBlkNo)
            src.OnLastBlk = src.OnBlk;
    }
}