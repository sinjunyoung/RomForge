using _3DS.Core.Crypto;
using _3DS.Core.FileSystem;
using _3DS.Core.Models;
using Common;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace _3DS.Core.Services;

public class CiaReader(KeyStore keyStore)
{
    private const int CiaAlign = 64;

    public async Task<CiaSource> OpenAsync(string ciaPath, Action<string, LogLevel, string>? log = null, CancellationToken ct = default)
    {
        var fileStream = File.Open(ciaPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        try
        {
            return await ParseAsync(fileStream, log, ct);
        }
        catch
        {
            await fileStream.DisposeAsync();
            throw;
        }
    }

    public async Task<CiaSource> ParseAsync(Stream stream, Action<string, LogLevel, string>? log = null, CancellationToken ct = default)
    {
        byte[] headerBuf = new byte[0x2020];

        stream.Position = 0;
        await stream.ReadExactlyAsync(headerBuf, ct);

        var ciaHeader = ParseCiaHeader(headerBuf);
        long certOffset = AlignUp(ciaHeader.ArchiveHeaderSize, CiaAlign);
        long ticketOffset = AlignUp(certOffset + ciaHeader.CertChainSize, CiaAlign);
        long tmdOffset = AlignUp(ticketOffset + ciaHeader.TicketSize, CiaAlign);
        long contentOffset = AlignUp(tmdOffset + ciaHeader.TmdSize, CiaAlign);
        byte[] ticketBuf = new byte[ciaHeader.TicketSize];

        stream.Position = ticketOffset;
        await stream.ReadExactlyAsync(ticketBuf, 0, (int)ciaHeader.TicketSize, ct);

        var ticket = ParseTicket(ticketBuf);
        byte[] titleKey = DecryptTitleKey(ticket);
        byte[] tmdBuf = new byte[ciaHeader.TmdSize];

        stream.Position = tmdOffset;
        await stream.ReadExactlyAsync(tmdBuf, 0, (int)ciaHeader.TmdSize, ct);

        var tmdHeader = TmdParser.Parse(tmdBuf);

        return new CiaSource
        {
            Stream = stream,
            CiaHeader = ciaHeader,
            Ticket = ticket,
            TitleKey = titleKey,
            TmdHeader = tmdHeader,
            TmdRaw = tmdBuf,
            TicketRaw = ticketBuf,
            KeyStore = keyStore,
            ContentOffset = contentOffset,
            Log = log,
        };
    }

    private static CiaHeader ParseCiaHeader(byte[] buf) => new()
    {
        ArchiveHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x00)),
        Type = BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(0x04)),
        Version = BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(0x06)),
        CertChainSize = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x08)),
        TicketSize = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x0C)),
        TmdSize = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x10)),
        MetaSize = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x14)),
        ContentSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x18)),
        ContentIndexBitmask = buf.AsSpan(0x20, 0x2000).ToArray(),
    };

    private static CiaTicket ParseTicket(byte[] buf)
    {
        uint sigType = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(0x00));
        int sigSize = GetSignatureSize(sigType, out int sigPadding);
        int dataOffset = 4 + sigSize + sigPadding;
        ulong titleId = BinaryPrimitives.ReadUInt64BigEndian(buf.AsSpan(dataOffset + 0x9C));
        byte commonKeyIndex = buf[dataOffset + 0xB1];
        byte[] encTitleKey = buf.AsSpan(dataOffset + 0x7F, 0x10).ToArray();

        return new CiaTicket
        {
            TitleId = titleId,
            CommonKeyIndex = commonKeyIndex,
            EncryptedTitleKey = encTitleKey,
        };
    }

    private byte[] DecryptTitleKey(CiaTicket ticket)
    {
        byte[] commonKey = keyStore.GetCommonKey(ticket.CommonKeyIndex);
        byte[] iv = new byte[16];

        BinaryPrimitives.WriteUInt64BigEndian(iv.AsSpan(0), ticket.TitleId);

        using var aes = Aes.Create();

        aes.Key = commonKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        using var dec = aes.CreateDecryptor();

        return dec.TransformFinalBlock(ticket.EncryptedTitleKey, 0, 16);
    }

    private static int GetSignatureSize(uint sigType, out int padding)
    {
        switch (sigType)
        {
            case 0x010003: case 0x010000: padding = 0x3C; return 0x200;
            case 0x010004: case 0x010001: padding = 0x3C; return 0x100;
            case 0x010005: case 0x010002: padding = 0x40; return 0x3C;
            default: throw new InvalidDataException($"알 수 없는 서명 타입: 0x{sigType:X8}");
        }
    }

    private static long AlignUp(long value, int alignment) => (value + alignment - 1) & ~(alignment - 1L);

    private static long AlignUp(uint value, int alignment) => AlignUp((long)value, alignment);
}