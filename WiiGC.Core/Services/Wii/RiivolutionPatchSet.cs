namespace WiiGC.Core.Services.Wii;

public sealed record RiivolutionPatchSet(string XmlPath, string SdRoot, IReadOnlyList<string> GameIds, IReadOnlyDictionary<string, string> Replacements, IReadOnlyList<string> Warnings)
{
    public bool MatchesDisc(string discId) => GameIds.Count == 0 || GameIds.Any(id => discId.StartsWith(id, StringComparison.OrdinalIgnoreCase));
}