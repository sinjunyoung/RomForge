namespace RomForge.Core.Models.N3DS;

public sealed record PatchSet(string Path, PatchKind Kind, IReadOnlyList<string> TargetIds, IReadOnlyList<string> Files)
{
    public int FileCount => Files.Count;

    public bool MatchesRom(string? titleId, string? productCode)
    {
        if (TargetIds.Count == 0)
            return true;

        if (!string.IsNullOrEmpty(titleId) && TargetIds.Any(id => titleId.Equals(id, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!string.IsNullOrEmpty(productCode) && TargetIds.Any(id => productCode.Equals(id, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}