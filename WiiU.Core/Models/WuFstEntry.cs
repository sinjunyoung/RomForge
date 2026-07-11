namespace WiiU.Core.Models;

public sealed class WuFstEntry
{
    public bool IsDirectory;
    public string Name = "";
    public int ParentDirIndex;
    public int DirEndIndex;
    public long FileOffsetField;
    public long FileSize;
    public int ClusterIndex;
}