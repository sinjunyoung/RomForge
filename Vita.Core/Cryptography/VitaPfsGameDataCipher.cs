using System.Security.Cryptography;

namespace Vita.Core.Cryptography;

public sealed class VitaPfsGameDataCipher
{
    private static readonly byte[] HmacKey0 =
    [
        0xE4, 0x62, 0x25, 0x8B, 0x1F, 0x31, 0x21, 0x56, 0x07, 0x45,
        0xDB, 0x62, 0xB1, 0x43, 0x67, 0x23, 0xD2, 0xBF, 0x80, 0xFE
    ];

    private readonly byte[] _drvKey;
    private readonly byte[] _tweakEncKey;
    private readonly int _blockSize;

    public VitaPfsGameDataCipher(VitaF00DEmulator f00d, ReadOnlySpan<byte> klicensee, ReadOnlySpan<byte> dbSeed, int blockSize)
    {
        if (klicensee.Length != 16)
            throw new ArgumentException("klicensee must be 16 bytes", nameof(klicensee));

        _drvKey = f00d.EncryptKey(klicensee);

        using var hmac = new HMACSHA1(HmacKey0);
        byte[] digest = hmac.ComputeHash(dbSeed.ToArray());

        _tweakEncKey = digest.AsSpan(0, 16).ToArray();

        _blockSize = blockSize;
    }

    public void DecryptRange(long absoluteOffset, Span<byte> buffer)
    {
        int offset = 0;

        while (offset < buffer.Length)
        {
            int chunk = Math.Min(_blockSize, buffer.Length - offset);
            long tweakKey = absoluteOffset + offset;
            byte[] tweak = new byte[16];

            WriteUInt64LittleEndian(tweak, (ulong)tweakKey);

            for (int i = 0; i < 16; i++)
                tweak[i] ^= _tweakEncKey[i];

            using var aes = Aes.Create();

            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = _drvKey;
            aes.IV = tweak;

            var block = buffer.Slice(offset, chunk).ToArray();

            if (chunk == _blockSize)
            {
                using var decryptor = aes.CreateDecryptor();
                var plain = new byte[chunk];

                decryptor.TransformBlock(block, 0, chunk, plain, 0);
                plain.CopyTo(buffer.Slice(offset, chunk));
            }
            else
                DecryptPartialBlockCbc(_drvKey, tweak, block).CopyTo(buffer.Slice(offset, chunk));

            offset += _blockSize;
        }
    }

    private static byte[] DecryptPartialBlockCbc(byte[] key, byte[] iv, byte[] data)
    {
        int fullByteCount = (data.Length / 16) * 16;
        int tail = data.Length - fullByteCount;
        var result = new byte[data.Length];
        byte[] ctsPrevBlock = iv;

        if (fullByteCount > 0)
        {
            using var aes = Aes.Create();

            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();

            decryptor.TransformBlock(data, 0, fullByteCount, result, 0);
            ctsPrevBlock = data.AsSpan(fullByteCount - 16, 16).ToArray();
        }

        if (tail > 0)
        {
            using var aes = Aes.Create();

            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = key;

            using var encryptor = aes.CreateEncryptor();
            var keystream = new byte[16];

            encryptor.TransformBlock(ctsPrevBlock, 0, 16, keystream, 0);

            for (int i = 0; i < tail; i++)
                result[fullByteCount + i] = (byte)(data[fullByteCount + i] ^ keystream[i]);
        }

        return result;
    }

    private static void WriteUInt64LittleEndian(byte[] dest, ulong value)
    {
        for (int i = 0; i < 8; i++)
            dest[i] = (byte)(value >> (8 * i));
    }
}