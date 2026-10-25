namespace WiiGC.Core.Models;

internal sealed class DecodedChunk
{
    public byte[] Data { get; set; } = [];

    public int Length { get; set; }

    public List<HashException>[] ExceptionLists { get; set; } = [];
}