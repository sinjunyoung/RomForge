namespace N3DS.Core.Models;

public class RomFsUnpackResult
{
    public required IvfcHeader IvfcHeader { get; init; }

    public required RomFsHeader RomFsHeader { get; init; }

    public required long DataLevel2Offset { get; init; }

    public required IReadOnlyList<RomFsDirNode> Directories { get; init; }

    public required IReadOnlyList<RomFsFileNode> Files { get; init; }
}