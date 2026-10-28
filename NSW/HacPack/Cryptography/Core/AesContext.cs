using NSW.HacPack.Cryptography.Algorithms;
using NSW.HacPack.Enums;
using System.Security.Cryptography;

namespace NSW.HacPack.Cryptography.Core;

public sealed class AesContext : IDisposable
{
    private readonly byte[] _key;
    private readonly AesMode _mode;
    private readonly byte[] _iv = new byte[16];
    private bool _disposed;

    private readonly Aes? _ecbAes;
    private readonly ICryptoTransform? _ecbEnc;
    private readonly ICryptoTransform? _ecbDec;

    public AesContext(ReadOnlySpan<byte> key, AesMode mode)
    {
        _key = key.ToArray();
        _mode = mode;

        if (mode == AesMode.ECB)
        {
            _ecbAes = Aes.Create();
            _ecbAes.Key = _key;
            _ecbAes.Mode = CipherMode.ECB;
            _ecbAes.Padding = PaddingMode.None;
            _ecbEnc = _ecbAes.CreateEncryptor();
            _ecbDec = _ecbAes.CreateDecryptor();
        }
    }

    public void SetIV(ReadOnlySpan<byte> iv)
    {
        if (iv.Length != 16)
            throw new ArgumentException("IV must be 16 bytes.");

        iv.CopyTo(_iv);
    }

    public void Encrypt(Span<byte> dst, ReadOnlySpan<byte> src)
    {
        switch (_mode)
        {
            case AesMode.ECB: ProcessEcb(dst, src, encrypt: true); break;
            case AesMode.CTR: ProcessCtr(dst, src); break;
            case AesMode.XTS: throw new InvalidOperationException("XTS는 XtsEncrypt 사용");
        }
    }

    private void ProcessEcb(Span<byte> dst, ReadOnlySpan<byte> src, bool encrypt)
    {
        var transform = encrypt ? _ecbEnc! : _ecbDec!;
        byte[] srcArr = src.ToArray();
        byte[] dstArr = new byte[srcArr.Length];
        const int blockSize = 16;

        for (int offset = 0; offset < srcArr.Length; offset += blockSize)
        {
            int len = Math.Min(blockSize, srcArr.Length - offset);

            transform.TransformBlock(srcArr, offset, len, dstArr, offset);
        }

        dstArr.AsSpan().CopyTo(dst);
    }

    private void ProcessCtr(Span<byte> dst, ReadOnlySpan<byte> src)
    {
        const int chunkSize = 0x400000;

        byte[] counterBlocks = new byte[chunkSize];
        byte[] keystream = new byte[chunkSize];
        Span<byte> counter = stackalloc byte[16];

        _iv.AsSpan().CopyTo(counter);

        using var aes = Aes.Create();

        aes.Key = _key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using var enc = aes.CreateEncryptor();
        int processed = 0;

        while (processed < src.Length)
        {
            int len = Math.Min(chunkSize, src.Length - processed);
            int blockCount = (len + 15) / 16;
            int totalBytes = blockCount * 16;

            for (int i = 0; i < blockCount; i++)
            {
                counter.CopyTo(counterBlocks.AsSpan(i * 16, 16));
                IncrementCounter(counter);
            }

            enc.TransformBlock(counterBlocks, 0, totalBytes, keystream, 0);

            for (int i = 0; i < len; i++)
                dst[processed + i] = (byte)(src[processed + i] ^ keystream[i]);

            processed += len;
        }
    }

    private void ProcessXts(Span<byte> dst, ReadOnlySpan<byte> src, bool encrypt, XtsAesContext xtsCtx)
    {
        byte[] T = xtsCtx.ComputeTweak(_iv);
        byte[] buf = new byte[16];
        byte[] cc = new byte[16];
        var transform = encrypt ? xtsCtx.Enc : xtsCtx.Dec;

        for (int offset = 0; offset < src.Length; offset += 16)
        {
            for (int j = 0; j < 16; j++)
                buf[j] = (byte)(src[offset + j] ^ T[j]);

            transform.TransformBlock(buf, 0, 16, cc, 0);

            for (int j = 0; j < 16; j++)
                dst[offset + j] = (byte)(cc[j] ^ T[j]);

            GfMul(T);
        }
    }

    private static void GenerateCmacSubkeys(ICryptoTransform enc, out byte[] K1, out byte[] K2)
    {
        byte[] L = enc.TransformFinalBlock(new byte[16], 0, 16);

        K1 = ShiftLeft(L);

        if ((L[0] & 0x80) != 0) 
            K1[15] ^= 0x87;

        K2 = ShiftLeft(K1);

        if ((K1[0] & 0x80) != 0) 
            K2[15] ^= 0x87;
    }

    private static void GetTweak(ulong sector, Span<byte> tweak)
    {
        for (int i = 15; i >= 0; i--)
        {
            tweak[i] = (byte)(sector & 0xFF);
            sector >>= 8;
        }
    }

    private static void IncrementCounter(Span<byte> counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
            if (++counter[i] != 0)
                break;
    }

    private static void GfMul(byte[] T)
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

    private static byte[] ShiftLeft(byte[] b)
    {
        byte[] r = new byte[16];

        for (int i = 0; i < 15; i++)
            r[i] = (byte)(b[i] << 1 | b[i + 1] >> 7);

        r[15] = (byte)(b[15] << 1);

        return r;
    }

    public void Dispose()
    {
        if (_disposed) 
            return;

        Array.Clear(_key, 0, _key.Length);
        Array.Clear(_iv, 0, _iv.Length);
        _ecbEnc?.Dispose();
        _ecbDec?.Dispose();
        _ecbAes?.Dispose();

        _disposed = true;
    }
}