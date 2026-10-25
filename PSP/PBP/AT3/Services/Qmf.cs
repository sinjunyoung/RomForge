namespace PSP.PBP.AT3.Services;

public sealed class Qmf(int nIn)
{
    private static readonly float[] QmfWindow = BuildWindow();
    private readonly float[] _pcmBuffer = new float[nIn + 46];
    private readonly float[] _pcmBufferMerge = new float[nIn + 46];

    private static float[] BuildWindow()
    {
        double[] tapHalf =
        [
            -0.00001461907, -0.00009205479, -0.000056157569, 0.00030117269, 0.0002422519, -0.00085293897, -0.0005205574, 0.0020340169, 0.00078333891, -0.0042153862, -0.00075614988, 0.0078402944,
            -0.000061169922, -0.01344162, 0.0024626821, 0.021736089, -0.007801671, -0.034090221, 0.01880949, 0.054326009, -0.043596379, -0.099384367, 0.13207909, 0.46424159
        ];

        var w = new float[48];

        for (int i = 0; i < 24; i++)
        {
            float tap = (float)tapHalf[i];

            w[i] = w[47 - i] = (float)(tap * 2.0);
        }

        return w;
    }

    public void Analysis(ReadOnlySpan<float> input, Span<float> lower, Span<float> upper)
    {
        float[] win = QmfWindow;
        float[] buf = _pcmBuffer;

        Array.Copy(buf, nIn, buf, 0, 46);
        input[..nIn].CopyTo(buf.AsSpan(46, nIn));

        for (int j = 0; j < nIn; j += 2)
        {
            float lo = 0.0f;
            float up = 0.0f;

            for (int i = 0; i < 24; i++)
            {
                lo += win[2 * i] * buf[48 - 1 + j - (2 * i)];
                up += win[(2 * i) + 1] * buf[48 - 1 + j - (2 * i) - 1];
            }

            float temp = up;

            up = lo - up;
            lo += temp;
            lower[j / 2] = lo;
            upper[j / 2] = up;
        }
    }

    public void Synthesis(Span<float> output, ReadOnlySpan<float> lower, ReadOnlySpan<float> upper)
    {
        float[] win = QmfWindow;
        float[] merge = _pcmBufferMerge;

        for (int i = 0; i < nIn; i += 4)
        {
            merge[46 + i + 0] = lower[i / 2] + upper[i / 2];
            merge[46 + i + 1] = lower[i / 2] - upper[i / 2];
            merge[46 + i + 2] = lower[i / 2 + 1] + upper[i / 2 + 1];
            merge[46 + i + 3] = lower[i / 2 + 1] - upper[i / 2 + 1];
        }

        int winP = 0;
        int outP = 0;

        for (int j = nIn / 2; j != 0; j--)
        {
            float s1 = 0.0f;
            float s2 = 0.0f;

            for (int i = 0; i < 48; i += 2)
            {
                s1 += merge[winP + i] * win[i];
                s2 += merge[winP + i + 1] * win[i + 1];
            }

            output[outP] = s2;
            output[outP + 1] = s1;
            winP += 2;
            outP += 2;
        }

        Array.Copy(merge, nIn, merge, 0, 46);
    }
}