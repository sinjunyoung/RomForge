namespace Vita.Core.Models;

public sealed class MergeResult
{
    public required int TotalFiles { get; init; }

    public required int PatchCandidates { get; init; }

    public required int PatchedSuccessfully { get; init; }
}