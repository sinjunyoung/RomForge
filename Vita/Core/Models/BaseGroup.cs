namespace Vita.Core.Models;

internal sealed class BaseGroup
{
    public required ContentCategory Category { get; init; }

    public required string TitleId { get; init; }

    public string? ContentIdSuffix { get; init; }

    public string? PatchPathOverride { get; init; }

    public required Dictionary<string, PatchAppEntry> Index { get; init; }

    public required List<OwnerContext> Owners { get; init; }
}
