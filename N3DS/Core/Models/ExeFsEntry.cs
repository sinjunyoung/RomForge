namespace N3DS.Core.Models;

public class ExeFsEntry
{
    public string Name { get; init; } = string.Empty;

    public uint Offset { get; init; }

    public uint Size { get; init; }
}