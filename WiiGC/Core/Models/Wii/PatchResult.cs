namespace WiiGC.Core.Models.Wii;

public sealed record PatchResult(IReadOnlyList<PatchEntry> Entries)
{
    public static PatchResult Empty { get; } = new([]);

    public int Total => Entries.Count;

    public int Applied => Entries.Count(e => e.Applied);

    public IReadOnlyList<string> Skipped => [.. Entries.Where(e => !e.Applied).Select(e => e.Path)];
}