namespace PBP.AT3.Services;

public abstract class MdctBase(int n, float scale)
{
    protected readonly int N = n;

    protected readonly float[] SinCos = CalcSinCos(n, scale);

    protected readonly float[] FftIn = new float[n >> 1];

    protected readonly float[] FftOut = new float[n >> 1];

    private readonly KissFft _fft = new(n >> 2);

    private static float[] CalcSinCos(int n, float scale)
    {
        var tmp = new float[n >> 1];
        float alpha = (float)(2.0 * Math.PI / (8.0 * n));
        float omiga = (float)(2.0 * Math.PI / n);

        scale = (float)Math.Sqrt(scale / n);

        for (int i = 0; i < (n >> 2); ++i)
        {
            float arg = omiga * i + alpha;

            tmp[2 * i + 0] = scale * MathF.Cos(arg);
            tmp[2 * i + 1] = scale * MathF.Sin(arg);
        }

        return tmp;
    }

    protected void RunFft() => _fft.Transform(FftIn, FftOut);
}