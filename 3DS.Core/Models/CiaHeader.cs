namespace _3DS.Core.Models;

public record struct CiaHeader
{
    public uint ArchiveHeaderSize { get; set; }
    public ushort Type { get; set; }
    public ushort Version { get; set; }
    public uint CertChainSize { get; set; }
    public uint TicketSize { get; set; }
    public uint TmdSize { get; set; }
    public uint MetaSize { get; set; }
    public ulong ContentSize { get; set; }
    public required byte[] ContentIndexBitmask { get; set; }

    public readonly bool IsContentPresent(int contentIndex)
    {
        int byteIndex = contentIndex / 8;
        int bitIndex = 7 - (contentIndex % 8);

        if (byteIndex < 0 || byteIndex >= ContentIndexBitmask.Length)
            return false;

        return (ContentIndexBitmask[byteIndex] & (1 << bitIndex)) != 0;
    }
}