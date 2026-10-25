namespace Vita.Core.Models;

public sealed class PkgProbeResult
{
    public required string TitleId { get; init; }

    public required ContentCategory Category { get; init; }

    public string? ContentIdSuffix { get; init; }

    public required string ContentId { get; init; }
}