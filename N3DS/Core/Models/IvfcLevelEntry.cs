namespace N3DS.Core.Models;

public record IvfcLevelEntry
{
    public ulong Offset { get; init; }

    public ulong Size { get; init; }

    public uint BlockSizeLog2 { get; init; }
}