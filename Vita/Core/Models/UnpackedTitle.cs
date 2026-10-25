namespace Vita.Core.Models;

public sealed record UnpackedTitle
{
    public required string TitleId { get; init; }

    public required string FolderPath { get; init; }

    public string? TitleName { get; init; }

    public byte[]? IconBytes { get; init; }

    public long SizeBytes { get; init; }

    public string? PatchPath { get; init; }

    public ContentCategory Category { get; init; }

    public string? ContentIdSuffix { get; init; }
}