namespace NSW.HacPack.Models;

public class RomfsCtx
{
    public RomfsFentCtx? Files;

    public ulong NumDirs;

    public ulong NumFiles;

    public ulong DirTableSize;

    public ulong FileTableSize;

    public ulong DirHashTableSize;

    public ulong FileHashTableSize;

    public ulong FilePartitionSize;
}