namespace PBP.AT3.Services;

internal sealed class KissFftReal
{
    private readonly KissFft _sub;
    private readonly int _ncfft;
    private readonly bool _inverse;
    private readonly float[] _tmp;
    private readonly float[] _superR;
    private readonly float[] _superI;

    public KissFftReal(int nfft, bool inverse)
    {
        if ((nfft & 1) != 0)
            throw new ArgumentException("Real FFT size must be even", nameof(nfft));

        _inverse = inverse;
        _ncfft = nfft >> 1;
        _sub = new KissFft(_ncfft, inverse);
        _tmp = new float[2 * _ncfft];
        _superR = new float[_ncfft / 2];
        _superI = new float[_ncfft / 2];

        for (int i = 0; i < _ncfft / 2; ++i)
        {
            double phase = -3.14159265358979323846264338327 * ((double)(i + 1) / _ncfft + .5);

            if (inverse)
                phase *= -1;

            _superR[i] = (float)Math.Cos(phase);
            _superI[i] = (float)Math.Sin(phase);
        }
    }

    public void Forward(float[] timeData, float[] freqData)
    {
        if (_inverse)
            throw new InvalidOperationException("Improper alloc");

        int ncfft = _ncfft;
        float[] tmp = _tmp;

        _sub.Transform(timeData, tmp);

        float tdcR = tmp[0];
        float tdcI = tmp[1];

        freqData[0] = tdcR + tdcI;
        freqData[2 * ncfft] = tdcR - tdcI;
        freqData[1] = 0.0f;
        freqData[2 * ncfft + 1] = 0.0f;

        for (int k = 1; k <= ncfft / 2; ++k)
        {
            float fpkR = tmp[2 * k];
            float fpkI = tmp[2 * k + 1];
            float fpnkR = tmp[2 * (ncfft - k)];
            float fpnkI = -tmp[2 * (ncfft - k) + 1];
            float f1kR = fpkR + fpnkR;
            float f1kI = fpkI + fpnkI;
            float f2kR = fpkR - fpnkR;
            float f2kI = fpkI - fpnkI;
            float sr = _superR[k - 1];
            float si = _superI[k - 1];
            float twR = f2kR * sr - f2kI * si;
            float twI = f2kR * si + f2kI * sr;

            freqData[2 * k] = (f1kR + twR) * 0.5f;
            freqData[2 * k + 1] = (f1kI + twI) * 0.5f;
            freqData[2 * (ncfft - k)] = (f1kR - twR) * 0.5f;
            freqData[2 * (ncfft - k) + 1] = (twI - f1kI) * 0.5f;
        }
    }

    public void Inverse(float[] freqData, float[] timeData)
    {
        if (!_inverse)
            throw new InvalidOperationException("Improper alloc");

        int ncfft = _ncfft;
        float[] tmp = _tmp;

        tmp[0] = freqData[0] + freqData[2 * ncfft];
        tmp[1] = freqData[0] - freqData[2 * ncfft];

        for (int k = 1; k <= ncfft / 2; ++k)
        {
            float fkR = freqData[2 * k];
            float fkI = freqData[2 * k + 1];
            float fnkcR = freqData[2 * (ncfft - k)];
            float fnkcI = -freqData[2 * (ncfft - k) + 1];
            float fekR = fkR + fnkcR;
            float fekI = fkI + fnkcI;
            float tR = fkR - fnkcR;
            float tI = fkI - fnkcI;
            float sr = _superR[k - 1];
            float si = _superI[k - 1];
            float fokR = tR * sr - tI * si;
            float fokI = tR * si + tI * sr;

            tmp[2 * k] = fekR + fokR;
            tmp[2 * k + 1] = fekI + fokI;
            tmp[2 * (ncfft - k)] = fekR - fokR;
            tmp[2 * (ncfft - k) + 1] = fekI - fokI;
            tmp[2 * (ncfft - k) + 1] *= -1;
        }

        _sub.Transform(tmp, timeData);
    }
}