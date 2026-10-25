namespace Vita.Core.Models;

public sealed class MergeGroup
{
    public required ContentCategory Category { get; init; }

    public required string TitleId { get; init; }

    public string? ContentIdSuffix { get; init; }

    public required PatchContext PatchCtx { get; init; }

    public required Dictionary<string, PatchAppEntry> Index { get; init; }

    public required List<PatchTarget> Targets { get; init; }

    public required List<OwnerContext> Owners { get; init; }
}