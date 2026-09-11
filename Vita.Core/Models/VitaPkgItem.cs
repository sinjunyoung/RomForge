namespace Vita.Core.Models;

public sealed class VitaPkgItem
{
    public required string Name { get; init; }

    public required long DataOffset { get; init; }

    public required long DataSize { get; init; }

    public required byte Flags { get; init; }
}