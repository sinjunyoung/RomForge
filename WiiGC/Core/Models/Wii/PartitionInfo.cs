namespace WiiGC.Core.Models.Wii;

internal sealed record PartitionInfo(string GameId, string Title, long DolOffset, long FstOffset, long FstSize, IReadOnlyList<FileEntry> Files);