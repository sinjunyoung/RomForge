namespace Vita.Core.Models;

public sealed class VitaPatchOnlyResult
{
    public required int MatchedCandidates { get; init; }

    public required int PatchedSuccessfully { get; init; }

    public required List<string> Messages { get; init; }
}