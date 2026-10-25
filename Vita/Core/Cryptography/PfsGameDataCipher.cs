using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Vita.Core.Cryptography;

public sealed class PfsGameDataCipher
{
    internal static readonly byte[] HmacKey0 = [ 0xE4, 0x62, 0x25, 0x8B, 0x1F, 0x31, 0x21, 0x56, 0x07, 0x45, 0xDB, 0x62, 0xB1, 0x43, 0x67, 0x23, 0xD2, 0xBF, 0x80, 0xFE ];

    private readonly byte[] _drvKey;
    private readonly byte[] _tweakEncKey;
    private readonly int _blockSize;

    public PfsGameDataCipher(F00DEmulator f00d, ReadOnlySpan<byte> klicensee, ReadOnlySpan<byte> dbSeed, int blockSize)
    {
        if (klicensee.Length != 16)
            throw new ArgumentException("klicensee must be 16 bytes", nameof(klicensee));

        _drvKey = f00d.EncryptKey(klicensee);
        _tweakEncKey = ComputeTweakEncKeyFromDbSeed(dbSeed);
        _blockSize = blockSize;
    }

    private PfsGameDataCipher(byte[] drvKey, byte[] tweakEncKey, int blockSize)
    {
        _drvKey = drvKey;
        _tweakEncKey = tweakEncKey;
        _blockSize = blockSize;
    }

    public static PfsGameDataCipher FromPrecomputedTweakKey(F00DEmulator f00d, ReadOnlySpan<byte> klicensee, byte[] precomputedTweakEncKey, int blockSize)
    {
        if (klicensee.Length != 16)
            throw new ArgumentException("klicensee must be 16 bytes", nameof(klicensee));

        return new PfsGameDataCipher(f00d.EncryptKey(klicensee), precomputedTweakEncKey, blockSize);
    }

    private static byte[] ComputeTweakEncKeyFromDbSeed(ReadOnlySpan<byte> dbSeed)
    {
        using var hmac = new HMACSHA1(HmacKey0);
        byte[] digest = hmac.ComputeHash(dbSeed.ToArray());

        return digest.AsSpan(0, 16).ToArray();
    }

    public void DecryptRange(long absoluteOffset, Span<byte> buffer)
    {
        using var aes = Aes.Create();

        aes.Key = _drvKey;

        Span<byte> tweak = stackalloc byte[16];
        Span<byte> previousCipherBlock = stackalloc byte[16];
        Span<byte> keystream = stackalloc byte[16];
        int offset = 0;

        while (offset < buffer.Length)
        {
            int chunk = Math.Min(_blockSize, buffer.Length - offset);
            var block = buffer.Slice(offset, chunk);

            tweak.Clear();
            BinaryPrimitives.WriteUInt64LittleEndian(tweak, (ulong)(absoluteOffset + offset));

            for (int i = 0; i < 16; i++)
                tweak[i] ^= _tweakEncKey[i];

            int fullByteCount = chunk / 16 * 16;
            int tail = chunk - fullByteCount;

            if (tail > 0)
            {
                if (fullByteCount > 0)
                    block.Slice(fullByteCount - 16, 16).CopyTo(previousCipherBlock);
                else
                    tweak.CopyTo(previousCipherBlock);
            }

            if (fullByteCount > 0)
            {
                var full = block[..fullByteCount];

                aes.DecryptCbc(full, tweak, full, PaddingMode.None);
            }

            if (tail > 0)
            {
                aes.EncryptEcb(previousCipherBlock, keystream, PaddingMode.None);

                for (int i = 0; i < tail; i++)
                    block[fullByteCount + i] ^= keystream[i];
            }

            offset += _blockSize;
        }
    }
}