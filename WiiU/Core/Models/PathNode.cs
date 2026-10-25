namespace WiiU.Core.Models;

public sealed class PathNode
{
    public bool IsFile;

    public uint NameIndex = uint.MaxValue;

    public readonly List<PathNode> Subnodes = [];

    public ulong FileOffset;

    public ulong FileSize;

    public uint NodeStartIndex;
}