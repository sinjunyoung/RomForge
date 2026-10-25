namespace N3DS.Core.Models;

public class RomFsDirNode
{
    public required string FullPath { get; init; }

    public required RomFsDirEntry Entry { get; init; }
}