namespace N3DS.Core.Models;

public class NcchUnpackResult
{
    public required NcchHeader Header { get; init; }

    public byte[]? ExHeader { get; init; }

    public byte[]? Logo { get; init; }

    public byte[]? PlainRegion { get; init; }

    public ExeFsUnpackResult? ExeFs { get; init; }

    public RomFsUnpackResult? RomFs { get; init; }
}