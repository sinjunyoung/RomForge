namespace DolphinTool.Core.Models;

internal sealed record WiiPartitionInfo(string GameId, string Title, long DolOffset, long FstOffset, long FstSize, IReadOnlyList<WiiFileEntry> Files);