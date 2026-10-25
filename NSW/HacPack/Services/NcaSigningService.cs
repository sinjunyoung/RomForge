using NSW.HacPack.Cryptography.Core;
using NSW.HacPack.Enums;
using NSW.HacPack.Models;
using System.IO;

namespace NSW.HacPack.Services;

public static class NcaSigningService
{
    public static void SignNca(ref NcaHeader hdr, NcaGenerationOptions settings)
    {
        byte[] headerBytes = NcaStructHelper.GetHeaderMagicBytes(ref hdr);

        if (string.IsNullOrEmpty(settings.NcaSignature1PrivateKey) || !File.Exists(settings.NcaSignature1PrivateKey))
            GenerateSig(hdr.FixedKeySig, settings);
        else
            RsaSigner.SignDataWithKeyFile(headerBytes, hdr.FixedKeySig, settings.NcaSignature1PrivateKey);
    }

    private static void GenerateSig(byte[] sig, NcaGenerationOptions settings)
    {
        switch (settings.NcaSig)
        {
            case NcaSigType.Static:
                for (int i = 0; i < 0x100; i++)
                    sig[i] = 4;

                break;
            case NcaSigType.Random:
                var rng = new Random();

                for (int i = 0; i < 0x100; i++) 
                    sig[i] = (byte)(rng.Next() % 0xFF);

                break;
            case NcaSigType.Zero:
                break;
        }
    }
}