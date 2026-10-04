namespace DolphinTool.Core.Models;

public sealed record WiiDiscInfo(string GameId, string Title, int DiscNumber, int Version, DiscContainerFormat Container, long FileSize, long DiscSize);