namespace NSW.HacPack.Models;

public class RomfsDirentCtx
{
    public string SumPath = string.Empty;

    public string CurPath = string.Empty;

    public uint EntryOffset;

    public RomfsDirentCtx? Parent;

    public RomfsDirentCtx? Child;

    public RomfsDirentCtx? Sibling;

    public RomfsFentCtx? File;

    public RomfsDirentCtx? Next;
}