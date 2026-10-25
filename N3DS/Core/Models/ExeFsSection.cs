namespace N3DS.Core.Models;

public struct ExeFsSection
{
    public long StartOffset;

    public long EndOffset;

    public byte[] Key;

    public uint CtrOffset;
}