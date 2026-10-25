namespace Vita.Core.Cryptography;

public static class ZrifDecoder
{
    private const int RifSize = 512;
    private const uint ZlibDictionaryIdZrif = 0x627d1d5d;

    private static readonly byte[] ZrifDict =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        48, 48, 48, 48, 57, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 48, 48, 48, 48,
        54, 48, 48, 48, 48, 55, 48, 48, 48, 48, 56, 0, 48, 48, 48, 48, 51, 48, 48, 48,
        48, 52, 48, 48, 48, 48, 53, 48, 95, 48, 48, 45, 65, 68, 68, 67, 79, 78, 84, 48,
        48, 48, 48, 50, 45, 80, 67, 83, 71, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 49,
        45, 80, 67, 83, 69, 48, 48, 48, 45, 80, 67, 83, 70, 48, 48, 48, 45, 80, 67, 83,
        67, 48, 48, 48, 45, 80, 67, 83, 68, 48, 48, 48, 45, 80, 67, 83, 65, 48, 48, 48,
        45, 80, 67, 83, 66, 48, 48, 48, 0, 1, 0, 1, 0, 1, 0, 2, 239, 205, 171, 137,
        103, 69, 35, 1
    ];

    public static byte[] Decode(string zrif)
    {
        byte[] raw = Base64Decode(zrif);
        return ZlibInflate(raw);
    }

    private static byte[] Base64Decode(string str)
    {
        string s = str.Trim();
        int pad = (4 - s.Length % 4) % 4;
        s = s.PadRight(s.Length + pad, '=');

        return Convert.FromBase64String(s);
    }

    private static byte[] ZlibInflate(byte[] input)
    {
        if (input.Length < 6)
            throw new InvalidDataException("zRIF length too short");

        if (((input[0] << 8) + input[1]) % 31 != 0)
            throw new InvalidDataException("zRIF header is corrupted");

        if ((input[0] & 0xf) != 8)
            throw new InvalidDataException("only deflate method supported in zRIF");

        int pos = 2;
        int dictLen = 0;
        byte[] outBuf = new byte[ZrifDict.Length + 1024];

        if ((input[1] & (1 << 5)) != 0)
        {
            if (outBuf.Length <= ZrifDict.Length)
                throw new InvalidDataException("output buffer too small");

            Array.Copy(ZrifDict, outBuf, ZrifDict.Length);

            dictLen = ZrifDict.Length;

            uint dictId = (uint)((input[2] << 24) | (input[3] << 16) | (input[4] << 8) | input[5]);

            if (dictId != ZlibDictionaryIdZrif)
                throw new InvalidDataException("zRIF uses unknown dictionary");

            pos += 4;
        }

        int slen = input.Length - pos - 4;
        var compressed = new byte[slen];

        Array.Copy(input, pos, compressed, 0, slen);

        int outLen = outBuf.Length;
        int srcLen = slen;
        var inflate = new PuffInflate();
        int err = inflate.Puff(dictLen, outBuf, ref outLen, compressed, ref srcLen);

        if (err != 0)
            throw new InvalidDataException($"failed to uncompress zRIF (err={err})");

        var result = new byte[outLen];

        Array.Copy(outBuf, dictLen, result, 0, outLen);

        uint expectedAdler = (uint)((input[pos + srcLen] << 24) | (input[pos + srcLen + 1] << 16) | (input[pos + srcLen + 2] << 8) | input[pos + srcLen + 3]);
        uint actualAdler = Adler32(result);

        if (actualAdler != expectedAdler)
            throw new InvalidDataException("zRIF is corrupted, wrong checksum");

        if (outLen != RifSize)
            throw new InvalidDataException("wrong size of zRIF, is it corrupted?");

        return result;
    }

    private static uint Adler32(byte[] data)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;

        foreach (byte c in data)
        {
            a = (a + c) % mod;
            b = (b + a) % mod;
        }

        return (b << 16) | a;
    }
}