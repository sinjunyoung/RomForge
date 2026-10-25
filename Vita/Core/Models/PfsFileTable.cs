namespace Vita.Core.Models;

public sealed class PfsFileTable
{
    public required List<PfsFlatEntry> Entries { get; init; }

    public required List<PfsUnicvEntry> UnicvEntries { get; init; }

    public required uint FilesSalt { get; init; }
}