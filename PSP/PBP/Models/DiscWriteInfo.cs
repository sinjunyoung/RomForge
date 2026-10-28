namespace PSP.PBP.Models;

public record DiscWriteInfo(Stream IsoStream, long IsoLength, string GameId, string GameTitle, byte[] TocData);