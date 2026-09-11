namespace Vita.Core.Models;

public sealed class VitaPfsFileTable
{
    public required List<PfsFlatEntry> Entries { get; init; }

    public required List<PfsUnicvEntry> UnicvEntries { get; init; }
}