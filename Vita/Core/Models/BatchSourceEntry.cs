namespace Vita.Core.Models;

public sealed class BatchSourceEntry
{
    public required SourceKind Kind { get; init; }

    public required string Path { get; init; }

    public string? License { get; init; }

    public string? PatchPath { get; init; }

    public PkgProbeResult? Probe { get; init; }

    public ContentCategory? ItemCategory { get; init; }

    public string? ItemTitleId { get; init; }

    public string? ItemContentIdSuffix { get; init; }

    public string? ItemSourcePath { get; init; }
}