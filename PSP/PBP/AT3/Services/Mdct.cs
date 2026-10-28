namespace PSP.PBP.AT3.Services;

public sealed class Mdct(int n, float scale = 1.0f) : MdctBase(n, scale)
{
    public void Transform(ReadOnlySpan<float> input, Span<float> output)
    {
        int n2 = N >> 1;
        int n4 = N >> 2;
        int n34 = 3 * n4;
        int n54 = 5 * n4;
        float[] sc = SinCos;
        float[] fi = FftIn;
        int n;

        for (n = 0; n < n4; n += 2)
        {
            float r0 = input[n34 - 1 - n] + input[n34 + n];
            float i0 = input[n4 + n] - input[n4 - 1 - n];
            float c = sc[n];
            float s = sc[n + 1];

            fi[n] = r0 * c + i0 * s;
            fi[n + 1] = i0 * c - r0 * s;
        }

        for (; n < n2; n += 2)
        {
            float r0 = input[n34 - 1 - n] - input[n - n4];
            float i0 = input[n4 + n] + input[n54 - 1 - n];
            float c = sc[n];
            float s = sc[n + 1];

            fi[n] = r0 * c + i0 * s;
            fi[n + 1] = i0 * c - r0 * s;
        }

        RunFft();

        float[] fo = FftOut;

        for (n = 0; n < n2; n += 2)
        {
            float r0 = fo[n];
            float i0 = fo[n + 1];
            float c = sc[n];
            float s = sc[n + 1];

            output[n] = -r0 * c - i0 * s;
            output[n2 - 1 - n] = -r0 * s + i0 * c;
        }
    }
}