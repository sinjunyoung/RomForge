using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

public static class Resampler
{
    private const int ZeroCrossings = 32;
    private const double CutoffFactor = 0.97;
    private const double KaiserBeta = 9.0;

    public static PcmData ToRate(PcmData input, int targetRate)
    {
        if (input.SampleRate == targetRate)
            return input;

        return new PcmData
        {
            Channels = input.Channels,
            SampleRate = targetRate,
            Samples = Resample(input.Samples, input.Channels, input.SampleRate, targetRate)
        };
    }

    public static float[] Resample(float[] interleaved, int channels, int srcRate, int dstRate)
    {
        if (srcRate <= 0 || dstRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(srcRate));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        long inFrames = interleaved.Length / channels;

        if (inFrames == 0)
            return [];

        long g = Gcd(srcRate, dstRate);
        int up = (int)(dstRate / g);
        int down = (int)(srcRate / g);
        double ratio = (double)dstRate / srcRate;
        double fc = 0.5 * Math.Min(1.0, ratio) * CutoffFactor;
        double half = ZeroCrossings / (2.0 * fc);
        int halfInt = (int)Math.Ceiling(half);
        int taps = 2 * halfInt;
        float[] table = BuildTable(up, taps, halfInt, fc, half);
        long outFrames = (inFrames * up + down - 1) / down;
        var output = new float[outFrames * channels];
        var channelData = new float[inFrames];
        var channelOut = new float[outFrames];

        for (int ch = 0; ch < channels; ch++)
        {
            for (long i = 0; i < inFrames; i++)
                channelData[i] = interleaved[i * channels + ch];

            ResampleChannel(channelData, inFrames, channelOut, outFrames, table, up, down, taps, halfInt);

            for (long i = 0; i < outFrames; i++)
                output[i * channels + ch] = channelOut[i];
        }

        return output;
    }

    private static void ResampleChannel(float[] x, long inFrames, float[] y, long outFrames, float[] table, int up, int down, int taps, int halfInt)
    {
        long pos = 0;

        for (long n = 0; n < outFrames; n++, pos += down)
        {
            long baseIdx = pos / up;
            int phase = (int)(pos - baseIdx * up);
            long first = baseIdx - halfInt + 1;
            int row = phase * taps;
            float acc = 0.0f;

            if (first >= 0 && first + taps <= inFrames)
            {
                var xs = new ReadOnlySpan<float>(x, (int)first, taps);
                var hs = new ReadOnlySpan<float>(table, row, taps);

                for (int j = 0; j < taps; j++)
                    acc += hs[j] * xs[j];
            }
            else
            {
                for (int j = 0; j < taps; j++)
                {
                    long k = first + j;

                    if (k >= 0 && k < inFrames)
                        acc += table[row + j] * x[k];
                }
            }

            y[n] = acc;
        }
    }

    private static float[] BuildTable(int up, int taps, int halfInt, double fc, double half)
    {
        var table = new float[(long)up * taps];
        double i0Beta = BesselI0(KaiserBeta);
        var row = new double[taps];

        for (int p = 0; p < up; p++)
        {
            double frac = (double)p / up;
            double sum = 0.0;

            for (int j = 0; j < taps; j++)
            {
                double u = frac + halfInt - 1 - j;
                double v = 0.0;

                if (Math.Abs(u) < half)
                {
                    double arg = 2.0 * fc * u;
                    double sinc = Math.Abs(arg) < 1e-12 ? 1.0 : Math.Sin(Math.PI * arg) / (Math.PI * arg);
                    double r = u / half;
                    double win = BesselI0(KaiserBeta * Math.Sqrt(Math.Max(0.0, 1.0 - r * r))) / i0Beta;

                    v = 2.0 * fc * sinc * win;
                }

                row[j] = v;
                sum += v;
            }

            for (int j = 0; j < taps; j++)
                table[(long)p * taps + j] = (float)(row[j] / sum);
        }
        return table;
    }

    private static double BesselI0(double x)
    {
        double sum = 1.0;
        double term = 1.0;
        double q = x * x / 4.0;

        for (int k = 1; k < 60; k++)
        {
            term *= q / ((double)k * k);
            sum += term;

            if (term < sum * 1e-17)
                break;
        }

        return sum;
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0)
        {
            long t = a % b;
            a = b;
            b = t;
        }

        return a;
    }
}