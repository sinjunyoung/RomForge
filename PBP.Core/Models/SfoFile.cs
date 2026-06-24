namespace PBP.Core.Models;

public class SfoFile
{
    public uint Magic { get; set; }

    public uint Version { get; set; }

    public uint KeyTableOffset { get; set; }

    public uint Padding { get; set; }

    public uint DataTableOffset { get; set; }

    public List<SfoIndexEntry> Entries { get; set; } = [];

    public uint Size { get; set; }
}