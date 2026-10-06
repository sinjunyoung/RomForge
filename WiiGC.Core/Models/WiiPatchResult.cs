namespace WiiGC.Core.Models;

public sealed record WiiPatchResult(IReadOnlyList<WiiPatchEntry> Entries)
{
    public static WiiPatchResult Empty { get; } = new([]);

    public int Total => Entries.Count;

    public int Applied => Entries.Count(e => e.Applied);

    public IReadOnlyList<string> Skipped => [.. Entries.Where(e => !e.Applied).Select(e => e.Path)];
}