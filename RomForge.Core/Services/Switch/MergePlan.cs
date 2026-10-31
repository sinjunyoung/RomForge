using RomForge.Core.Models.Switch;

namespace RomForge.Core.Services.Switch;

internal sealed record MergePlan(string BaseTitleId, MergeEntry Base, MergeEntry? Update, IReadOnlyList<MergeEntry> Dlcs)
{
    public List<string> AllPaths => [.. new[] { Base.Path }
    .Concat(Update != null ? [Update.Path] : [])
    .Concat(Dlcs.Select(d => d.Path))
    .Distinct(StringComparer.OrdinalIgnoreCase)];
}