using LibHac.Crypto;
using NSW.HacPack.Models;
using System.IO;

namespace NSW.HacPack.Services;

public static class TicketService
{
    private static readonly byte[] CertData = TicketFiles.Cert;
    private static readonly byte[] TikData = TicketFiles.Tik;

    public static void GenerateCertificate(NcaGenerationOptions settings)
    {
        string certPath = Path.Combine(settings.OutDirectory, $"{settings.TitleId:x16}{(byte)settings.KeyGeneration:x16}.cert");

        File.WriteAllBytes(certPath, CertData);
    }

    public static void GenerateTicket(NcaGenerationOptions settings)
    {
        string tikPath = Path.Combine(settings.OutDirectory,

        $"{settings.TitleId:x16}{(byte)settings.KeyGeneration:x16}.tik");

        var titleKek = settings.KeySet.TitleKeks[settings.KeyGeneration - 1];
        byte[] encKey = new byte[0x10];

        Aes.EncryptEcb128(settings.TitleKey.AsSpan(), encKey.AsSpan(), titleKek);

        byte[] tik = (byte[])TikData.Clone();

        Array.Copy(encKey, 0, tik, 0x180, 0x10);

        tik[0x285] = (byte)settings.KeyGeneration;

        byte[] rightsId = new byte[0x10];

        for (int i = 0; i < 8; i++)
            rightsId[7 - i] = (byte)(settings.TitleId >> 8 * i & 0xFF);

        rightsId[15] = (byte)settings.KeyGeneration;

        Array.Copy(rightsId, 0, tik, 0x2A0, 0x10);
        File.WriteAllBytes(tikPath, tik);
    }
}