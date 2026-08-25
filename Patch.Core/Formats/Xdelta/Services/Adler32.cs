namespace Patch.Core.Formats.Xdelta.Services;

public static class Adler32
{
    private const uint Base = 65521;
    private const int NMax = 5552;

    public static uint Compute(uint adler, ReadOnlySpan<byte> buf)
    {
        uint s1 = adler & 0xffff;
        uint s2 = (adler >> 16) & 0xffff;
        int len = buf.Length;
        int offset = 0;

        while (len > 0)
        {
            int k = len < NMax ? len : NMax;
            len -= k;

            while (k >= 16)
            {
                for (int i = 0; i < 16; i++)
                {
                    s1 += buf[offset + i];
                    s2 += s1;
                }
                offset += 16;
                k -= 16;
            }

            while (k > 0)
            {
                s1 += buf[offset];
                s2 += s1;
                offset++;
                k--;
            }

            s1 %= Base;
            s2 %= Base;
        }

        return (s2 << 16) | s1;
    }
}