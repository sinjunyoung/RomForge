using PBP.AT3.IO;
using PBP.AT3.Models;

namespace PBP.AT3.Services;

internal static class MantissaReader
{
    private static readonly short[][] HuffLut = BuildHuffLut();
    private static readonly int[] ClcInverse = new int[4];
    private static readonly int[] VlcPairFirst = new int[9];
    private static readonly int[] VlcPairSecond = new int[9];

    static MantissaReader()
    {
        for (int m = -2; m <= 1; m++)
            ClcInverse[CodecInfo.MantissaToClcIdx(m)] = m;

        for (int a = -1; a <= 1; a++)
        {
            for (int b = -1; b <= 1; b++)
            {
                int idx = (int)CodecInfo.MantissasToVlcIndex(a, b);

                VlcPairFirst[idx] = a;
                VlcPairSecond[idx] = b;
            }
        }
    }

    private static short[][] BuildHuffLut()
    {
        var tables = CodecInfo.HuffTables;
        var result = new short[tables.Length][];

        for (int t = 0; t < tables.Length; t++)
        {
            var lut = new short[9 << 8];

            Array.Fill(lut, (short)-1);

            for (int s = 0; s < tables[t].Length; s++)
                lut[(tables[t][s].Bits << 8) | tables[t][s].Code] = (short)s;

            result[t] = lut;
        }

        return result;
    }

    private static int ReadHuffman(BitStreamReader br, short[] lut)
    {
        int code = 0;

        for (int bits = 1; bits <= 8; bits++)
        {
            code = (code << 1) | (int)br.ReadBit();

            short symbol = lut[(bits << 8) | code];

            if (symbol >= 0)
                return symbol;
        }

        throw new InvalidDataException("허프만 코드가 올바르지 않습니다.");
    }

    public static void Read(BitStreamReader br, int selector, bool constantLength, int count, Span<int> output)
    {
        if (constantLength)
        {
            int numBits = CodecInfo.ClcLengthTab[selector];

            if (selector > 1)
            {
                for (int i = 0; i < count; i++)
                    output[i] = numBits == 0 ? 0 : br.ReadSigned(numBits);
            }
            else
            {
                for (int i = 0; i < count / 2; i++)
                {
                    int code = (int)br.Read(numBits);

                    output[i * 2] = ClcInverse[code >> 2];
                    output[i * 2 + 1] = ClcInverse[code & 3];
                }
            }

            return;
        }

        var lut = HuffLut[selector - 1];

        if (selector > 1)
        {
            for (int i = 0; i < count; i++)
            {
                int symbol = ReadHuffman(br, lut) + 1;
                int value = symbol >> 1;

                output[i] = (symbol & 1) != 0 ? -value : value;
            }
        }
        else
        {
            for (int i = 0; i < count / 2; i++)
            {
                int symbol = ReadHuffman(br, lut);

                output[i * 2] = VlcPairFirst[symbol];
                output[i * 2 + 1] = VlcPairSecond[symbol];
            }
        }
    }
}