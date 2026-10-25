namespace Vita.Core.Models;

public sealed class PfsFlatEntry
{
    public required uint Index { get; init; }

    public required uint ParentIndex { get; init; }

    public required string Name { get; init; }

    public required PfsFileType Type { get; init; }

    public required uint Size { get; init; }

    public string? RelativePath { get; set; }
}