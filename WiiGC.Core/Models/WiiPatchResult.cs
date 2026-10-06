namespace WiiGC.Core.Models;

public sealed record WiiPatchResult(int Total, int Applied, IReadOnlyList<string> Skipped)
{
    public static WiiPatchResult Empty { get; } = new(0, 0, []);
}