using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DolphinTool.Core.Services.Wii;

internal static class WiiTicket
{
    private const int TicketSize = 0x2A4;
    private const int TitleKeyOffset = 0x1BF;
    private const int TitleIdOffset = 0x1DC;
    private const int CommonKeyIndexOffset = 0x1F1;

    public static int Size => TicketSize;

    public static byte[] DecryptTitleKey(ReadOnlySpan<byte> ticket)
    {
        if (ticket.Length < TicketSize)
            throw new InvalidDataException("Wii 티켓 크기가 올바르지 않습니다.");

        int index = ticket[CommonKeyIndexOffset];

        if (index >= WiiKeys.CommonKeys.Length)
            index = 0;

        Span<byte> iv = stackalloc byte[16];

        ticket.Slice(TitleIdOffset, 8).CopyTo(iv);

        using var aes = Aes.Create();

        aes.Key = WiiKeys.CommonKeys[index];

        byte[] titleKey = new byte[16];

        aes.DecryptCbc(ticket.Slice(TitleKeyOffset, 16), iv, titleKey, PaddingMode.None);

        return titleKey;
    }

    public static ulong ReadTitleId(ReadOnlySpan<byte> ticket) => BinaryPrimitives.ReadUInt64BigEndian(ticket[TitleIdOffset..]);
}