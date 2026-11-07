namespace RomForge.Core.Models.Switch;

public sealed record Lz4FileEstimate(Lz4EstimateKind Kind, long FileBytes = 0, long OutputBytes = 0, long MarginBytes = 0)
{
    public static Lz4FileEstimate NotApplicable { get; } = new(Lz4EstimateKind.NotApplicable);

    public double SavedFraction => FileBytes > 0 ? (double)(FileBytes - OutputBytes) / FileBytes : 0;
}