using System.Security.Cryptography;

namespace Vita.Core.Cryptography;

public static class VitaPfsLegacyKeyDerivation
{
    public static byte[] ComputeTweakEncKey(uint filesSalt, uint icvSalt)
    {
        byte[] message = filesSalt == 0 ? WriteUInt32LittleEndian(icvSalt) : Concat(WriteUInt32LittleEndian(filesSalt), WriteUInt32LittleEndian(icvSalt));
        using var hmac = new HMACSHA1(VitaPfsGameDataCipher.HmacKey0);
        byte[] digest = hmac.ComputeHash(message);

        return digest.AsSpan(0, 16).ToArray();
    }

    private static byte[] WriteUInt32LittleEndian(uint value) =>
    [
        (byte)value,
        (byte)(value >> 8),
        (byte)(value >> 16),
        (byte)(value >> 24)
    ];

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length];

        a.CopyTo(result, 0);
        b.CopyTo(result, a.Length);

        return result;
    }
}