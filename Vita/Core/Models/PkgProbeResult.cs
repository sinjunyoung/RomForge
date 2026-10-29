namespace Vita.Core.Models;

public sealed class PkgProbeResult
{
    public string? TitleId { get; init; }

    public ContentCategory? Category { get; init; }

    public string? ContentIdSuffix { get; init; }

    public string? ContentId { get; init; }
}