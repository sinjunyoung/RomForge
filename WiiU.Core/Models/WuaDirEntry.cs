namespace WiiU.Core.Models;

public sealed class WuaDirEntry
{
    public bool IsFile;
    public bool IsDirectory => !IsFile;
    public string Name = "";
    public ulong Size;
}