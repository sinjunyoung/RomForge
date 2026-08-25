namespace Patch.Core.Formats.Xdelta.Models;

public struct DjwBitState
{
    public byte CurByte;
    public uint CurMask;

    public static DjwBitState DecodeInit() => new() { CurByte = 0, CurMask = 0x100 };
}