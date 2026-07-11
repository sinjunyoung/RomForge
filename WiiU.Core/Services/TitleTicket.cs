// TitleTicket.cs
//
// C# port of Cemu's NCrypto::ETicketParser (src/Cemu/ncrypto/ncrypto.cpp / .h),
// verified directly against the cemu-project/Cemu source.
//
// Ticket layout (offsets confirmed against Cemu's ETicketBody struct):
//   0x1BF : encryptedTitleKey (16 bytes)
//   0x1DC : titleIdHigh (uint32 BE)
//   0x1E0 : titleIdLow  (uint32 BE)
//   (together, titleIdHigh<<32 | titleIdLow forms the 8-byte big-endian title ID)
//
// The Wii U common key used to decrypt the title key is hardcoded directly in Cemu's own
// open-source repository (ncrypto.cpp, ETicketParser::GetTitleKey) — it is not something
// RomForge needs to ask the user for. This class simply mirrors that.

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace WiiU.Core.Services;

public sealed class TitleTicket
{
    private const int EncryptedTitleKeyOffset = 0x1BF;
    private const int TitleIdHighOffset = 0x1DC;
    private const int TitleIdLowOffset = 0x1E0;

    // Same constant Cemu's open-source code hardcodes (src/Cemu/ncrypto/ncrypto.cpp).
    private static readonly byte[] WiiUCommonKey =
    {
        0xD7, 0xB0, 0x04, 0x02, 0x65, 0x9B, 0xA2, 0xAB,
        0xD2, 0xCB, 0x0D, 0xB2, 0x7F, 0xA2, 0xB6, 0x56
    };

    public ulong TitleId { get; }
    public byte[] EncryptedTitleKey { get; }

    private TitleTicket(ulong titleId, byte[] encryptedTitleKey)
    {
        TitleId = titleId;
        EncryptedTitleKey = encryptedTitleKey;
    }

    public static TitleTicket Parse(ReadOnlySpan<byte> ticketData)
    {
        if (ticketData.Length < TitleIdLowOffset + 4)
            throw new InvalidDataException("Ticket data is too short.");

        var encryptedTitleKey = ticketData.Slice(EncryptedTitleKeyOffset, 16).ToArray();
        uint titleIdHigh = BinaryPrimitives.ReadUInt32BigEndian(ticketData.Slice(TitleIdHighOffset, 4));
        uint titleIdLow = BinaryPrimitives.ReadUInt32BigEndian(ticketData.Slice(TitleIdLowOffset, 4));
        ulong titleId = ((ulong)titleIdHigh << 32) | titleIdLow;

        return new TitleTicket(titleId, encryptedTitleKey);
    }

    /// <summary>Decrypts and returns the real 16-byte title key, using the hardcoded Wii U common key.</summary>
    public byte[] DecryptTitleKey()
    {
        Span<byte> iv = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(iv, TitleId);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = WiiUCommonKey;
        aes.IV = iv.ToArray();

        using var decryptor = aes.CreateDecryptor();
        var titleKey = new byte[16];
        decryptor.TransformBlock(EncryptedTitleKey, 0, 16, titleKey, 0);
        return titleKey;
    }

    public string TitleIdHex => TitleId.ToString("x16");
}
