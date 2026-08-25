namespace Patch.Core.Formats.Xdelta.Models;

public static class Xd3Constants
{
    public const uint VcdSecondary = 1U << 0;
    public const uint VcdCodeTable = 1U << 1;
    public const uint VcdAppHeader = 1U << 2;
    public const uint VcdInvHdr = ~0x7U;

    public const uint VcdSource = 1U << 0;
    public const uint VcdTarget = 1U << 1;
    public const uint VcdAdler32 = 1U << 2;
    public const uint VcdInvWin = ~0x7U;

    public const uint VcdSrcOrTgt = VcdSource | VcdTarget;

    public const uint VcdDataComp = 1U << 0;
    public const uint VcdInstComp = 1U << 1;
    public const uint VcdAddrComp = 1U << 2;
    public const uint VcdInvDel = ~0x7U;

    public const byte Xd3Noop = 0;
    public const byte Xd3Add = 1;
    public const byte Xd3Run = 2;

    public const uint DefaultWinSize = 1U << 23;
    public const uint DefaultSPrevSz = 1U << 18;
    public const uint AllocSize = 1U << 14;
    public const long MaxSrcWinSz = 1L << 61;

    public const byte VcdMagic1 = 0xd6;
    public const byte VcdMagic2 = 0xc3;
    public const byte VcdMagic3 = 0xc4;

    public const uint VcdSelf = 0;
    public const uint VcdHere = 1;

    public const byte Xd3Cpy = 3;
    public const byte MinMatch = 4;
}