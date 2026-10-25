using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

public static class PsyCommon
{
    private static readonly short[] AthTab = [ 
        9669, 9669, 9626, 9512, 9353, 9113, 8882, 8676, 8469, 8243, 7997, 7748, 7492, 7239, 7000, 6762, 6529, 6302, 6084, 5900, 5717, 5534, 5351, 5167, 5004, 4812, 4638, 4466, 4310, 4173, 4050, 3922, 3723, 3577, 3451, 3281, 3132, 3036, 2902, 2760, 
        2658, 2591, 2441, 2301, 2212, 2125, 2018, 1900, 1770, 1682, 1594, 1512, 1430, 1341, 1260, 1198, 1136, 1057, 998, 943, 887, 846, 744, 712, 693, 668, 637, 606, 580, 555, 529, 502, 475, 448, 422, 398, 375, 351, 327, 322,
        312, 301, 291, 268, 246, 215, 182, 146, 107, 61, 13, -35, -96, -156, -179, -235, -295, -350, -401, -421, -446, -499, -532, -535, -513, -476, -431, -313, -179, 8, 203, 403, 580, 736, 881, 1022, 1154, 1251, 1348, 1421,
        1479, 1399, 1285, 1193, 1287, 1519, 1914, 2369, 3352, 4352, 5352, 6352, 7352, 8352, 9352, 9999, 9999, 9999, 9999, 9999 ];

    private static double AthFormulaFrank(float freq)
    {
        if (freq < 10.0f) 
            freq = 10.0f;

        if (freq > 29853.0f) 
            freq = 29853.0f;

        double freqLog = 40.0 * Math.Log10(0.1 * freq);
        uint index = (uint)freqLog;

        return 0.01 * (AthTab[index] * (1 + index - freqLog) + AthTab[index + 1] * (freqLog - index));
    }

    public static float[] CalcAth(int len, int sampleRate)
    {
        var res = new float[len];
        float mf = (float)((float)sampleRate / 2000.0);

        for (int i = 0; i < res.Length; i++)
        {
            float f = (float)(i + 1) * mf / len;
            float trh = (float)(AthFormulaFrank((float)(1.0e3 * f)) - 100);

            trh = (float)(trh - f * f * 0.015);
            res[i] = trh;
        }

        return res;
    }

    public static float AnalizeScaleFactorSpread(IReadOnlyList<ScaledBlock> scaledBlocks)
    {
        float s = 0.0f;

        for (int i = 0; i < scaledBlocks.Count; ++i)
            s += scaledBlocks[i].ScaleFactorIndex;

        s /= scaledBlocks.Count;

        float sigma = 0.0f;
        float t;

        for (int i = 0; i < scaledBlocks.Count; ++i)
        {
            t = scaledBlocks[i].ScaleFactorIndex - s;
            t *= t;
            sigma += t;
        }

        sigma /= scaledBlocks.Count;
        sigma = MathF.Sqrt(sigma);

        if (sigma > 14.0)
            sigma = 14.0f;

        return (float)(sigma / 14.0);
    }

    public static float[] CreateLoudnessCurve(int sz)
    {
        var res = new float[sz];

        for (int i = 0; i < sz; i++)
        {
            float f = (float)((float)(i + 3) * 0.5 * 44100 / (float)sz);
            float t = (float)(MathF.Log10(f) - 3.5);

            t = -10 * t * t + 3 - f / 3000;
            t = (float)Math.Pow(10, 0.1 * t);
            res[i] = t;
        }

        return res;
    }

    public static float[] CalcSpectralFlatnessPerBfu(float[] mdctEnergy, int[] specsStart, int[] specsPerBlock, int numBfu, float energyFloor = 1e-12f)
    {
        float floor = MathF.Max(energyFloor, 1e-20f);
        var flatness = new float[numBfu];

        Array.Fill(flatness, 1.0f);

        for (int bfu = 0; bfu < numBfu; ++bfu)
        {
            int start = specsStart[bfu];
            int len = specsPerBlock[bfu];
            int end = start + len;

            if (len == 0 || end > mdctEnergy.Length)
            {
                flatness[bfu] = 1.0f;
                continue;
            }

            double arithMean = 0.0;
            double meanLog = 0.0;

            for (int i = start; i < end; ++i)
            {
                double e = MathF.Max(0.0f, mdctEnergy[i]);

                arithMean += e;
                meanLog += Math.Log(Math.Max(e, (double)floor));
            }

            arithMean /= len;
            meanLog /= len;

            if (arithMean <= floor)
            {
                flatness[bfu] = 1.0f;
                continue;
            }

            double geomMean = Math.Exp(meanLog);
            double ratio = geomMean / arithMean;

            flatness[bfu] = (float)Math.Min(1.0, Math.Max(0.0, ratio));
        }

        return flatness;
    }

    public static float TrackLoudness(float prevLoud, float l0, float l1) => (float)(0.98 * prevLoud + 0.01 * (l0 + l1));

    public static float TrackLoudness(float prevLoud, float l) => (float)(0.98 * prevLoud + 0.02 * l);
}