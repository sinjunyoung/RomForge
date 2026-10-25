using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

public static class Scaler
{
    private const float MaxScale = 1.0f;

    public static ScaledBlock Scale(ReadOnlySpan<float> input)
    {
        float maxAbsSpec = 0;

        for (int i = 0; i < input.Length; ++i)
        {
            float absSpec = MathF.Abs(input[i]);

            if (absSpec > maxAbsSpec)
                maxAbsSpec = absSpec;
        }

        if (maxAbsSpec > MaxScale)
            maxAbsSpec = MaxScale;

        float[] table = CodecInfo.ScaleTable;
        int idx = LowerBound(table, maxAbsSpec);
        float scaleFactor = table[idx];
        var res = new ScaledBlock
        {
            ScaleFactorIndex = (byte)idx,
            Values = new float[input.Length],
            Energy = 0.0f
        };

        for (int i = 0; i < input.Length; ++i)
        {
            float scaledValue = input[i] / scaleFactor;
            float energy = input[i] * input[i];

            res.Energy += energy;

            if (Math.Abs(scaledValue) >= 1.0)
                scaledValue = (scaledValue > 0) ? (float)0.99999 : (float)-0.99999;

            res.Values[i] = scaledValue;
        }

        return res;
    }

    public static List<ScaledBlock> ScaleFrame(ReadOnlySpan<float> specs)
    {
        var blocks = new List<ScaledBlock>(CodecInfo.MaxBfus);

        for (int band = 0; band < CodecInfo.NumQmf; ++band)
        {
            for (int block = CodecInfo.BlocksPerBand[band]; block < CodecInfo.BlocksPerBand[band + 1]; ++block)
            {
                int start = CodecInfo.BlockSizeTab[block];
                int len = CodecInfo.SpecsPerBlock[block];

                blocks.Add(Scale(specs.Slice(start, len)));
            }
        }

        return blocks;
    }

    private static int LowerBound(float[] table, float value)
    {
        int lo = 0;
        int hi = table.Length - 1;

        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;

            if (table[mid] < value)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }
}