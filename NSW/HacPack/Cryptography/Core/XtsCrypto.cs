using System.Security.Cryptography;

namespace NSW.HacPack.Cryptography.Core;

public static class XtsCrypto
{
    public static void Encrypt(byte[] data, byte[] key1, byte[] key2, ulong sector)
    {
        byte[] T = GetEncryptedTweak(key2, sector);
        using var aes = Aes.Create();

        aes.Key = key1;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using var enc = aes.CreateEncryptor();
        byte[] buf = new byte[16];

        for (int i = 0; i < data.Length; i += 16)
        {
            for (int j = 0; j < 16; j++) 
                buf[j] = (byte)(data[i + j] ^ T[j]);

            byte[] cc = enc.TransformFinalBlock(buf, 0, 16);

            for (int j = 0; j < 16; j++) 
                data[i + j] = (byte)(cc[j] ^ T[j]);

            MultiplyGaloisField(T);
        }
    }

    private static byte[] GetEncryptedTweak(byte[] key2, ulong sector)
    {
        byte[] tweak = new byte[16];
        ulong tmp = sector;

        for (int i = 15; i >= 0; i--)
        {
            tweak[i] = (byte)(tmp & 0xFF);
            tmp >>= 8;
        }

        using var aes = Aes.Create();

        aes.Key = key2;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        return aes.CreateEncryptor().TransformFinalBlock(tweak, 0, 16);
    }

    private static void MultiplyGaloisField(byte[] T)
    {
        byte carry = 0;

        for (int i = 0; i < 16; i++)
        {
            byte next = (byte)(T[i] >> 7);

            T[i] = (byte)(T[i] << 1 | carry);
            carry = next;
        }

        if (carry != 0) 
            T[0] ^= 0x87;
    }
}