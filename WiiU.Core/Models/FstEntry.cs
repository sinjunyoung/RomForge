namespace WiiU.Core.Models;

public sealed class FstEntry
{
    public bool IsDirectory;
    public string Name = string.Empty;
    public int ParentDirIndex;
    public int DirEndIndex;
    public uint FileOffsetField;
    public uint FileSize;
    public ushort ClusterIndex;
    public bool IsSharedWithBase;
}