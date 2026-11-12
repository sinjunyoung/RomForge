using Common.Localization;
using System.Security.Cryptography;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal static class Ticket
{
    private const int TicketSize = 0x2A4;
    private const int TitleKeyOffset = 0x1BF;
    private const int TitleIdOffset = 0x1DC;
    private const int CommonKeyIndexOffset = 0x1F1;

    public static int Size => TicketSize;

    public static byte[] DecryptTitleKey(ReadOnlySpan<byte> ticket)
    {
        if (ticket.Length < TicketSize)
            throw new InvalidDataException(Loc.S.ErrWiiTicketSizeInvalid);

        int index = ticket[CommonKeyIndexOffset];

        if (index >= Keys.CommonKeys.Length)
            throw new NotSupportedException(Loc.S.ErrUnsupportedWiiCommonKeyIndex(index));

        Span<byte> iv = stackalloc byte[16];

        ticket.Slice(TitleIdOffset, 8).CopyTo(iv);

        using var aes = Aes.Create();

        aes.Key = Keys.CommonKeys[index];

        byte[] titleKey = new byte[16];

        aes.DecryptCbc(ticket.Slice(TitleKeyOffset, 16), iv, titleKey, PaddingMode.None);

        return titleKey;
    }
}