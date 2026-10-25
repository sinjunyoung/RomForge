namespace N3DS.Core.Models;

public class RomFsDirEntry
{
    public uint ParentOffset { get; init; }

    public uint SiblingOffset { get; init; }

    public uint ChildDirOffset { get; init; }

    public uint ChildFileOffset { get; init; }

    public uint HashSiblingOffset { get; init; }

    public uint NameSize { get; init; }

    public string Name { get; init; } = string.Empty;

    public const uint None = 0xFFFFFFFF;

    public const int FixedSize = 0x18;

    public int TotalSize => FixedSize + (int)AlignUp(NameSize, 4);

    private static uint AlignUp(uint v, uint a) => (v + a - 1) & ~(a - 1);
}