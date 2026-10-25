using System.Security.Cryptography;
using System.Text;

namespace N3DS.Core.Crypto;

public class SdCrypto
{
    private readonly KeyStore _keyStore;

    public SdCrypto(KeyStore keyStore)
    {
        _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));

        if (!keyStore.IsMovableLoaded)
            throw new InvalidOperationException("LoadMovable 먼저 호출 필요");
    }

    public static byte[] PathToIv(string sdPath)
    {
        byte[] encoded = Encoding.Unicode.GetBytes(sdPath.ToLowerInvariant() + "\0");
        byte[] hash = SHA256.HashData(encoded);
        byte[] ctr = new byte[16];

        for (int i = 0; i < 16; i++)
            ctr[i] = (byte)(hash[i] ^ hash[16 + i]);

        return ctr;
    }

    public byte[] Decrypt(string sdPath, byte[] encrypted)
    {
        byte[] key = _keyStore.GetSdKey();
        byte[] iv = PathToIv(sdPath);

        return AesCtr(encrypted, key, iv);
    }

    public byte[] Encrypt(string sdPath, byte[] plaintext)
    {
        return Decrypt(sdPath, plaintext);
    }

    public byte[] DecryptFile(string filePath, string sdPath)
    {
        byte[] encrypted = File.ReadAllBytes(filePath);

        return Decrypt(sdPath, encrypted);
    }

    public void EncryptToFile(string filePath, string sdPath, byte[] plaintext)
    {
        byte[] encrypted = Encrypt(sdPath, plaintext);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllBytes(filePath, encrypted);
    }

    public void DecryptStream(string sdPath, Stream input, Stream output, long length = -1)
    {
        byte[] key = _keyStore.GetSdKey();
        byte[] iv = PathToIv(sdPath);
        long toRead = length < 0 ? input.Length - input.Position : length;

        AesCtrStream(input, output, key, iv, toRead);
    }

    public void EncryptStream(string sdPath, Stream input, Stream output, long length = -1) => DecryptStream(sdPath, input, output, length);

    public byte[] ComputeCmac(int keySlot, byte[] data)
    {
        byte[] key = _keyStore.GetNormalKey(keySlot);

        return AesCmac(key, data);
    }

    public byte[] GetKey() => _keyStore.GetSdKey();


    private static byte[] AesCtr(byte[] data, byte[] key, byte[] iv)
    {
        byte[] result = new byte[data.Length];
        using var aes = Aes.Create();

        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        byte[] ctr = (byte[])iv.Clone();
        byte[] keystream = new byte[16];
        byte[] ctrBuffer = new byte[16];
        int offset = 0;

        while (offset < data.Length)
        {
            Buffer.BlockCopy(ctr, 0, ctrBuffer, 0, 16);

            encryptor.TransformBlock(ctrBuffer, 0, 16, keystream, 0);

            int chunk = Math.Min(16, data.Length - offset);

            for (int i = 0; i < chunk; i++)
                result[offset + i] = (byte)(data[offset + i] ^ keystream[i]);

            IncrementCtr(ctr);

            offset += chunk;
        }

        return result;
    }

    private static void AesCtrStream(Stream input, Stream output, byte[] key, byte[] iv, long length)
    {
        const int BufferSize = 0x200000;

        using var aes = Aes.Create();

        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        byte[] ctr = (byte[])iv.Clone();
        byte[] keystream = new byte[16];
        byte[] ctrBuffer = new byte[16];
        byte[] buffer = new byte[BufferSize];
        long remaining = length;

        while (remaining > 0)
        {
            int toRead = (int)Math.Min(BufferSize, remaining);
            int bytesRead = input.ReadAtLeast(buffer, toRead, throwOnEndOfStream: false);

            if (bytesRead == 0) 
                break;

            int pos = 0;

            while (pos < bytesRead)
            {
                Buffer.BlockCopy(ctr, 0, ctrBuffer, 0, 16);
                encryptor.TransformBlock(ctrBuffer, 0, 16, keystream, 0);

                int chunk = Math.Min(16, bytesRead - pos);

                for (int i = 0; i < chunk; i++)
                    buffer[pos + i] ^= keystream[i];

                IncrementCtr(ctr);

                pos += chunk;
            }

            output.Write(buffer, 0, bytesRead);

            remaining -= bytesRead;
        }
    }

    public static byte[] AesCmac(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();

        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        byte[] L = new byte[16];

        encryptor.TransformBlock(new byte[16], 0, 16, L, 0);

        byte[] K1 = GenerateSubKey(L);
        byte[] K2 = GenerateSubKey(K1);
        int blockCount = Math.Max(1, (data.Length + 15) / 16);
        bool lastBlockComplete = data.Length > 0 && data.Length % 16 == 0;
        byte[] lastBlock = new byte[16];

        if (lastBlockComplete)
        {
            Buffer.BlockCopy(data, (blockCount - 1) * 16, lastBlock, 0, 16);
            XorBlock(lastBlock, K1);
        }
        else
        {
            int lastBlockSize = data.Length % 16;

            if (lastBlockSize > 0)
                Buffer.BlockCopy(data, (blockCount - 1) * 16, lastBlock, 0, lastBlockSize);

            lastBlock[lastBlockSize] = 0x80;

            XorBlock(lastBlock, K2);
        }

        byte[] x = new byte[16];

        for (int i = 0; i < blockCount - 1; i++)
        {
            XorBlock(x, data, i * 16);
            encryptor.TransformBlock(x, 0, 16, x, 0);
        }

        XorBlock(x, lastBlock);
        encryptor.TransformBlock(x, 0, 16, x, 0);

        return x;
    }

    private static byte[] GenerateSubKey(byte[] key)
    {
        byte[] subKey = new byte[16];
        bool msb = (key[0] & 0x80) != 0;

        for (int i = 0; i < 15; i++)
            subKey[i] = (byte)((key[i] << 1) | (key[i + 1] >> 7));

        subKey[15] = (byte)(key[15] << 1);

        if (msb) 
            subKey[15] ^= 0x87;

        return subKey;
    }

    private static void XorBlock(byte[] target, byte[] source, int sourceOffset = 0)
    {
        for (int i = 0; i < 16; i++)
            target[i] ^= source[sourceOffset + i];
    }

    public static void IncrementCtr(byte[] ctr)
    {
        for (int i = 15; i >= 0; i--)
            if (++ctr[i] != 0) 
                break;
    }
}