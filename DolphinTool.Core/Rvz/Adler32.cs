namespace DolphinTool.Core.Rvz;

internal static class Adler32
{
    private const uint Modulus = 65521;
    private const int MaxBlock = 5552;

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint a = 1;
        uint b = 0;
        int index = 0;

        while (index < data.Length)
        {
            int count = Math.Min(MaxBlock, data.Length - index);

            for (int i = 0; i < count; i++)
            {
                a += data[index + i];
                b += a;
            }

            a %= Modulus;
            b %= Modulus;
            index += count;
        }

        return (b << 16) | a;
    }
}