namespace Vita.Core.Models;

public sealed class PkgBatchEntry
{
    public required string PkgPath { get; init; }

    public required string License { get; init; }

    public string? PatchPath { get; init; }

    public required PkgProbeResult Probe { get; init; }
}