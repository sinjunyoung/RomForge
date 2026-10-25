namespace N3DS.Core.Models;

public record struct CiaTicket
{
    public ulong TitleId { get; set; }

    public byte CommonKeyIndex { get; set; }

    public byte[] EncryptedTitleKey { get; set; }

    public CiaTicket() => EncryptedTitleKey = new byte[16];
}