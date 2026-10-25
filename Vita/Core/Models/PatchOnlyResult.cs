namespace Vita.Core.Models;

public sealed class PatchOnlyResult
{
    public required int MatchedCandidates { get; init; }

    public required int PatchedSuccessfully { get; init; }
}