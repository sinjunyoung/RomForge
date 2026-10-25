namespace PSP.PBP.AT3.Services;

public sealed class Midct
{
    public const int InputSize = 256;
    public const int OutputSize = 512;

    private const int Half = InputSize / 2;

    private readonly KissFft _fft = new(Half);
    private readonly float[] _twiddle = BuildTwiddle();
    private readonly float[] _fftIn = new float[InputSize];
    private readonly float[] _fftOut = new float[InputSize];
    private readonly float[] _dct = new float[InputSize];

    private static float[] BuildTwiddle()
    {
        var t = new float[InputSize];

        for (int i = 0; i < Half; i++)
        {
            double a = -Math.PI * (i + 0.125) / InputSize;

            t[2 * i] = (float)Math.Cos(a);
            t[2 * i + 1] = (float)Math.Sin(a);
        }

        return t;
    }

    public void Transform(ReadOnlySpan<float> input, Span<float> output)
    {
        for (int n = 0; n < Half; n++)
        {
            float re = input[2 * n];
            float im = input[InputSize - 1 - 2 * n];
            float c = _twiddle[2 * n];
            float s = _twiddle[2 * n + 1];

            _fftIn[2 * n] = re * c - im * s;
            _fftIn[2 * n + 1] = re * s + im * c;
        }

        _fft.Transform(_fftIn, _fftOut);

        for (int k = 0; k < Half; k++)
        {
            float re = _fftOut[2 * k];
            float im = _fftOut[2 * k + 1];
            float c = _twiddle[2 * k];
            float s = _twiddle[2 * k + 1];

            _dct[2 * k] = re * c - im * s;
            _dct[InputSize - 1 - 2 * k] = -(re * s + im * c);
        }

        for (int n = 0; n < 128; n++)
            output[n] = 2.0f * _dct[n + 128];

        for (int n = 128; n < 384; n++)
            output[n] = -2.0f * _dct[383 - n];

        for (int n = 384; n < 512; n++)
            output[n] = -2.0f * _dct[n - 384];
    }
}