namespace N3DS.Core.Models;

public class RomFsFileNode
{
    public required string FullPath { get; init; }

    public required ulong DataOffset { get; init; }

    public required ulong DataSize { get; init; }

    public required RomFsFileEntry Entry { get; init; }
}