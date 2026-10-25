namespace Patch.Core.Formats.DCP.Models;

public class Iso9660Entry
{
    public string Name { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public List<Iso9660Entry> Children { get; } = [];

    public string FullPath { get; set; } = string.Empty;

    public List<(uint Lba, uint Size)> Extents { get; } = [];

    public uint Lba => Extents.Count > 0 ? Extents[0].Lba : 0;

    public uint Size => (uint)Extents.Sum(e => e.Size);

    public uint LayoutLba { get; set; }

    public uint LayoutSize { get; set; }

    public uint ParentLba { get; set; }

    public uint ParentSize { get; set; }

    public int PendingRecordBytes { get; set; }
}