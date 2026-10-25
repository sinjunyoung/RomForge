namespace Vita.Core.Models;

public sealed class PatchAppEntry
{
    public required OwnerContext Owner { get; init; }

    public required int EntryIndex { get; init; }

    public PfsFlatEntry FileEntry => Owner.Table.Entries[EntryIndex];
}