namespace WiiGC.Core.Models;

public sealed record WiiPatchEntry(string Path, bool Applied, long Offset = 0);