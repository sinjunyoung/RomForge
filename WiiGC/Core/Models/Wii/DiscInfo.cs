using WiiGC.Core.Enums;

namespace WiiGC.Core.Models.Wii;

public sealed record DiscInfo(string GameId, string Title, int DiscNumber, int Version, DiscContainerFormat Container, long FileSize, long DiscSize);