namespace NSW.HacPack.Models;

public class RomfsFentCtx
{
    public string SumPath = string.Empty;

    public string CurPath = string.Empty;

    public uint EntryOffset;

    public ulong Offset;

    public ulong Size;

    public RomfsDirentCtx? Parent;

    public RomfsFentCtx? Sibling;

    public RomfsFentCtx? Next;
}