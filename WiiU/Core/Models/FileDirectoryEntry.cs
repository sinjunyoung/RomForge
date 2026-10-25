namespace WiiU.Core.Models;

public sealed class FileDirectoryEntry
{
    public bool IsFile;

    public uint NameOffset;

    public ulong FileOffset;

    public ulong FileSize;

    public uint NodeStartIndex;

    public uint Count;
}