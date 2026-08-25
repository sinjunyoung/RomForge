namespace Patch.Core.Formats.Xdelta.Models;

public struct Xd3Dinst(byte type1, byte size1, byte type2, byte size2)
{
    public byte Type1 = type1;
    public byte Size1 = size1;
    public byte Type2 = type2;
    public byte Size2 = size2;
}