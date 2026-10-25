namespace Patch.Core.Formats.PC98.Models;

public sealed class Fat16DirEntry
{
    public required string Name { get; init; }

    public required byte Attr { get; init; }

    public ushort Cluster { get; set; }

    public uint Size { get; set; }

    public required int EntryOffset { get; init; }

    public bool IsDirectory => (Attr & 0x10) != 0;
}