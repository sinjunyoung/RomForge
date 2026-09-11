using System.Security.Cryptography;

namespace Vita.Core.Cryptography;

public sealed class VitaAes128Ctr : IDisposable
{
    private readonly Aes _aes;
    private readonly ICryptoTransform _ecbEncryptor;
    private readonly byte[] _baseIv;

    public VitaAes128Ctr(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        if (key.Length != 16) 
            throw new ArgumentException("key must be 16 bytes", nameof(key));

        if (iv.Length != 16) 
            throw new ArgumentException("iv must be 16 bytes", nameof(iv));

        _aes = Aes.Create();
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;
        _aes.Key = key.ToArray();
        _ecbEncryptor = _aes.CreateEncryptor();
        _baseIv = iv.ToArray();
    }

    public void XorAt(long blockOffset, Span<byte> buffer)
    {
        var counter = new byte[16];

        AddCounter(_baseIv, blockOffset, counter);

        var keystream = new byte[16];
        int processed = 0;

        while (processed < buffer.Length)
        {
            _ecbEncryptor.TransformBlock(counter, 0, 16, keystream, 0);

            int chunk = Math.Min(16, buffer.Length - processed);

            for (int i = 0; i < chunk; i++)
                buffer[processed + i] ^= keystream[i];

            processed += chunk;

            IncrementCounter(counter);
        }
    }

    public static byte[] EcbEncryptSingleBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> block)
    {
        using var aes = Aes.Create();

        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key.ToArray();

        using var encryptor = aes.CreateEncryptor();
        var input = block.ToArray();
        var output = new byte[16];

        encryptor.TransformBlock(input, 0, 16, output, 0);

        return output;
    }

    private static void AddCounter(byte[] baseIv, long blockOffset, byte[] result)
    {
        Array.Copy(baseIv, result, 16);

        ulong add = (ulong)blockOffset;
        int carry = 0;

        for (int i = 15; i >= 0 && (add != 0 || carry != 0); i--)
        {
            int sum = result[i] + (int)(add & 0xFF) + carry;

            result[i] = (byte)sum;
            carry = sum >> 8;
            add >>= 8;
        }
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (int i = 15; i >= 0; i--)
        {
            if (++counter[i] != 0)
                break;
        }
    }

    public void Dispose()
    {
        _ecbEncryptor.Dispose();
        _aes.Dispose();
    }
}