namespace WiiGC.Core.Models.Wii;

public sealed record PatchEntry(string Path, bool Applied, long Offset = 0);