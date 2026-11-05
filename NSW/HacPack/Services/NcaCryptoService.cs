using LibHac.Crypto;
using NSW.HacPack.Cryptography.Core;
using NSW.HacPack.Models;

namespace NSW.HacPack.Services;

public static class NcaCryptoService
{
    public static void EncryptKeyArea(ref NcaHeader hdr, NcaGenerationOptions settings)
    {
        int keygenIdx = settings.KeyGeneration - 1;
        var kaek = settings.KeySet.KeyAreaKeys[keygenIdx][0];
        byte[] keys = new byte[0x40];

        Array.Copy(hdr.EncryptedKeys, keys, 0x40);
        Aes.EncryptEcb128(keys.AsSpan(), keys.AsSpan(), kaek);
        Array.Copy(keys, hdr.EncryptedKeys, 0x40);
    }

    public static void EncryptHeader(ref NcaHeader hdr, NcaGenerationOptions settings)
    {
        byte[] rawHdr = NcaStructHelper.StructToBytes(ref hdr);
        byte[] key1 = settings.KeySet.HeaderKey.DataKey.DataRo.ToArray();
        byte[] key2 = settings.KeySet.HeaderKey.TweakKey.DataRo.ToArray();

        for (ulong sector = 0; sector < 6; sector++)
        {
            int offset = (int)(sector * 0x200);
            byte[] block = new byte[0x200];

            Array.Copy(rawHdr, offset, block, 0, 0x200);
            XtsCrypto.Encrypt(block, key1, key2, sector);
            Array.Copy(block, 0, rawHdr, offset, 0x200);
        }

        NcaStructHelper.BytesToStruct(rawHdr, ref hdr);
    }
}