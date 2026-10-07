namespace WiiU.Core.Models;

public sealed class WuFstEntry
{
    public bool IsDirectory;
    public string Name = string.Empty;
    public int ParentDirIndex;
    public int DirEndIndex;
    public long FileOffsetField;
    public long FileSize;
    public int ClusterIndex;
}