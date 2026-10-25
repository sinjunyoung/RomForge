namespace N3DS.Core.Models;

public class ExeFsUnpackResult
{
    public required ExeFsHeader Header { get; init; }

    public required IReadOnlyList<ExeFsFile> Files { get; init; }
}