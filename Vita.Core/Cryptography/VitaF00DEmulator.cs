using System.Security.Cryptography;

namespace Vita.Core.Cryptography;

public sealed class VitaF00DEmulator
{
    private static readonly byte[] ContractKey0 = [0xE1, 0x22, 0x13, 0xB4, 0x80, 0x16, 0xB0, 0xE9, 0x9A, 0xB8, 0x1F, 0x8E, 0xC0, 0x2A, 0xD4, 0xA2];

    private readonly Dictionary<string, byte[]> _cache = [];

    public byte[] EncryptKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 16 && key.Length != 32)
            throw new ArgumentException("key must be 16 or 32 bytes", nameof(key));

        string cacheKey = Convert.ToHexString(key);

        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        using var aes = Aes.Create();
        
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = ContractKey0;

        using var decryptor = aes.CreateDecryptor();
        var input = key.ToArray();
        var output = new byte[input.Length];
        
        decryptor.TransformBlock(input, 0, input.Length, output, 0);

        _cache[cacheKey] = output;

        return output;
    }
}