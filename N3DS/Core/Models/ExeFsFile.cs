namespace N3DS.Core.Models;

public class ExeFsFile
{
    public required string Name { get; init; }

    public required byte[] Data { get; init; }

    public required byte[] ExpectedHash { get; init; }

    public bool HashValid { get; init; }
}