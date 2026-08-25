namespace Patch.Core.Formats.Xdelta.Models;

public sealed class Xd3Desect
{
    public byte[]? Buf;
    public int BufOffset;
    public int Size;
    public int Pos;

    public byte[]? Copied1;
    public byte[]? Copied2;
}