namespace N3DS.Core.Models;

public record struct Z3dsHeader
{
    public byte[] UnderlyingMagic;

    public byte Version;

    public ushort HeaderSize;

    public uint MetadataSize;

    public long CompressedSize;

    public long UncompressedSize;
}