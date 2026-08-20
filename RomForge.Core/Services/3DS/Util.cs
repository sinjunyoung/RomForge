using _3DS.Core.Crypto;
using _3DS.Core.Enums;
using _3DS.Core.IO;
using _3DS.Core.Models;
using _3DS.Core.Services;
using RomForge.Core.Models._3DS;
using System.Buffers.Binary;
using System.IO;

namespace RomForge.Core.Services._3DS;

public static class Util
{
    public static async Task<TitleParseResult> ParseFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();

        InstalledTitle? title = null;
        SmdhInfo? smdhInfo = null;
        NcchHeader? ncchHeader = null;
        KeyStore keyStore = KeyStoreProvider.Instance.KeyStore;

        switch (ext)
        {
            case ".cia":
                {
                    await using var stream = File.OpenRead(path);
                    var reader = new CiaReader(keyStore);
                    var ciaSource = await reader.ParseAsync(stream);

                    await ciaSource.LoadGameInfoAsync();
                    smdhInfo = ciaSource.SmdhInfo;
                    ncchHeader = ciaSource.MainNcchHeader;
                    title = new InstalledTitle
                    {
                        Contents = [.. ciaSource.Contents],
                        TitleId = ciaSource.TmdHeader.TitleId.ToString("x16"),
                        Version = ciaSource.TmdHeader.Version,
                        ContentSize = (ulong)new FileInfo(path).Length,
                        ContentPath = path,
                        Type = (TitleType)(ciaSource.TmdHeader.TitleId >> 32)
                    };
                    break;
                }
            case ".cci":
            case ".3ds":
                {
                    await using var stream = File.OpenRead(path);

                    (ncchHeader, smdhInfo) = await ParseNcchFromCciStreamAsync(stream, keyStore);
                    title = TitleFromNcch(ncchHeader!, path);
                    break;
                }
            case ".zcci":
                {
                    await using var fileStream = File.OpenRead(path);
                    var z3dsHeader = Z3dsArchiveService.ParseZ3dsHeader(fileStream);
                    await using var decompStream = new ZcciDecompressStream(fileStream, z3dsHeader);

                    (ncchHeader, smdhInfo) = await ParseNcchFromCciStreamAsync(decompStream, keyStore);
                    title = TitleFromNcch(ncchHeader!, path);
                    break;
                }
        }

        return new TitleParseResult
        {
            Title = title!,
            FilePath = path,
            ProductCode = ncchHeader?.ProductCodeString ?? string.Empty,
            ShortDescription = smdhInfo?.ShortDescription ?? string.Empty,
            Publisher = smdhInfo?.Publisher ?? string.Empty,
            Crypto = ncchHeader != null && !ncchHeader.NoCrypto,
            IconPixels = smdhInfo?.IconPixels,
            AvailableLanguages = smdhInfo?.AvailableLanguages ?? []
        };
    }

    public static InstalledTitle TitleFromNcch(NcchHeader h, string path) => new()
    {
        TitleId = h.ProgramId.ToString("x16"),
        Version = h.Version,
        ContentSize = (ulong)new FileInfo(path).Length,
        ContentPath = path,
        Type = (TitleType)(h.ProgramId >> 32)
    };

    public static async Task<(NcchHeader ncchHeader, SmdhInfo? smdhInfo)> ParseNcchFromCciStreamAsync(Stream stream, KeyStore keyStore)
    {
        byte[] ncsdBuf = new byte[0x200];

        stream.Position = 0x100;

        await stream.ReadExactlyAsync(ncsdBuf);

        uint partOffset = BinaryPrimitives.ReadUInt32LittleEndian(ncsdBuf.AsSpan(0x20, 4));
        uint partSize = BinaryPrimitives.ReadUInt32LittleEndian(ncsdBuf.AsSpan(0x24, 4));
        long ncchOffset = partOffset * 0x200L;
        long ncchSize = partSize * 0x200L;
        byte[] ncchHeaderBuf = new byte[0x200];

        stream.Position = ncchOffset;

        await stream.ReadExactlyAsync(ncchHeaderBuf);

        var ncchHeader = NcchHeader.Parse(ncchHeaderBuf, 0);
        Stream ncchStream = new SubStream(stream, ncchOffset, ncchSize);

        if (!ncchHeader.NoCrypto)
            ncchStream = new NcchDecryptionStream(ncchStream, 0, keyStore);

        SmdhInfo? smdhInfo;
        await using (ncchStream)
            smdhInfo = await NcchGameInfoReader.LoadAsync(ncchStream);

        return (ncchHeader, smdhInfo);
    }
}