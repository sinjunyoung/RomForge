using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PSP.PBP.AT3.Services;

internal sealed class KissFft
{
    private readonly int _nfft;
    private readonly float[] _twr;
    private readonly float[] _twi;
    private readonly int[] _factors;
    private readonly bool _inverse;

    public KissFft(int nfft, bool inverse = false)
    {
        if (nfft < 2 || (nfft & (nfft - 1)) != 0)
            throw new ArgumentException("FFT size must be a power of two", nameof(nfft));

        _nfft = nfft;
        _inverse = inverse;
        _twr = new float[nfft];
        _twi = new float[nfft];

        for (int i = 0; i < nfft; i++)
        {
            double phase = -2 * Math.PI * i / nfft;

            if (inverse)
                phase *= -1;

            _twr[i] = (float)Math.Cos(phase);
            _twi[i] = (float)Math.Sin(phase);
        }

        _factors = Factor(nfft);
    }

    private static int[] Factor(int n)
    {
        var list = new List<int>();
        int p = 4;

        do
        {
            while (n % p != 0)
                p = p == 4 ? 2 : throw new InvalidOperationException();

            n /= p;

            list.Add(p);
            list.Add(n);
        } while (n > 1);

        return [.. list];
    }

    public void Transform(float[] fin, float[] fout)
    {
        if (ReferenceEquals(fin, fout))
            throw new ArgumentException("In-place transform is not supported");

        if (fin.Length < 2 * _nfft || fout.Length < 2 * _nfft)
            throw new ArgumentException("Buffers are too small");

        Work(fout, 0, fin, 0, 1, 1, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Work(float[] fout, int foutIdx, float[] fin, int finIdx, int fstride, int inStride, int fi)
    {
        int p = _factors[fi++];
        int m = _factors[fi++];
        int begin = foutIdx;
        int end = foutIdx + p * m;

        if (m == 1)
        {
            ref float o = ref MemoryMarshal.GetArrayDataReference(fout);
            ref float i = ref MemoryMarshal.GetArrayDataReference(fin);
            int step = fstride * inStride;

            do
            {
                Unsafe.Add(ref o, 2 * foutIdx) = Unsafe.Add(ref i, 2 * finIdx);
                Unsafe.Add(ref o, 2 * foutIdx + 1) = Unsafe.Add(ref i, 2 * finIdx + 1);

                finIdx += step;
            } while (++foutIdx != end);
        }
        else
        {
            do
            {
                Work(fout, foutIdx, fin, finIdx, fstride * p, inStride, fi);

                finIdx += fstride * inStride;
            } while ((foutIdx += m) != end);
        }

        if (p == 2)
            Bfly2(fout, begin, fstride, m);
        else
            Bfly4(fout, begin, fstride, m);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Bfly2(float[] fa, int idx, int fstride, int m)
    {
        ref float f = ref MemoryMarshal.GetArrayDataReference(fa);
        ref float twr = ref MemoryMarshal.GetArrayDataReference(_twr);
        ref float twi = ref MemoryMarshal.GetArrayDataReference(_twi);
        int f2 = idx + m;
        int tw = 0;

        do
        {
            float f2r = Unsafe.Add(ref f, 2 * f2);
            float f2i = Unsafe.Add(ref f, 2 * f2 + 1);
            float wr = Unsafe.Add(ref twr, tw);
            float wi = Unsafe.Add(ref twi, tw);
            float tr = f2r * wr - f2i * wi;
            float ti = f2r * wi + f2i * wr;

            tw += fstride;

            Unsafe.Add(ref f, 2 * f2) = Unsafe.Add(ref f, 2 * idx) - tr;
            Unsafe.Add(ref f, 2 * f2 + 1) = Unsafe.Add(ref f, 2 * idx + 1) - ti;
            Unsafe.Add(ref f, 2 * idx) += tr;
            Unsafe.Add(ref f, 2 * idx + 1) += ti;

            f2++;
            idx++;
        } while (--m != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Bfly4(float[] fa, int idx, int fstride, int m)
    {
        ref float f = ref MemoryMarshal.GetArrayDataReference(fa);
        ref float twr = ref MemoryMarshal.GetArrayDataReference(_twr);
        ref float twi = ref MemoryMarshal.GetArrayDataReference(_twi);
        bool inverse = _inverse;
        int k = m;
        int m2 = 2 * m;
        int m3 = 3 * m;
        int tw1 = 0, tw2 = 0, tw3 = 0;
        int s2 = fstride * 2;
        int s3 = fstride * 3;

        do
        {
            int a = 2 * (idx + m);
            int b = 2 * (idx + m2);
            int c = 2 * (idx + m3);
            int o = 2 * idx;
            float ar = Unsafe.Add(ref f, a);
            float ai = Unsafe.Add(ref f, a + 1);
            float br = Unsafe.Add(ref f, b);
            float bi = Unsafe.Add(ref f, b + 1);
            float cr = Unsafe.Add(ref f, c);
            float ci = Unsafe.Add(ref f, c + 1);
            float w1r = Unsafe.Add(ref twr, tw1);
            float w1i = Unsafe.Add(ref twi, tw1);
            float w2r = Unsafe.Add(ref twr, tw2);
            float w2i = Unsafe.Add(ref twi, tw2);
            float w3r = Unsafe.Add(ref twr, tw3);
            float w3i = Unsafe.Add(ref twi, tw3);
            float s0r = ar * w1r - ai * w1i;
            float s0i = ar * w1i + ai * w1r;
            float s1r = br * w2r - bi * w2i;
            float s1i = br * w2i + bi * w2r;
            float s2r = cr * w3r - ci * w3i;
            float s2i = cr * w3i + ci * w3r;
            float or0 = Unsafe.Add(ref f, o);
            float oi0 = Unsafe.Add(ref f, o + 1);
            float s5r = or0 - s1r;
            float s5i = oi0 - s1i;
            float nor = or0 + s1r;
            float noi = oi0 + s1i;
            float s3r = s0r + s2r;
            float s3i = s0i + s2i;
            float s4r = s0r - s2r;
            float s4i = s0i - s2i;

            Unsafe.Add(ref f, b) = nor - s3r;
            Unsafe.Add(ref f, b + 1) = noi - s3i;

            tw1 += fstride;
            tw2 += s2;
            tw3 += s3;

            Unsafe.Add(ref f, o) = nor + s3r;
            Unsafe.Add(ref f, o + 1) = noi + s3i;

            if (inverse)
            {
                Unsafe.Add(ref f, a) = s5r - s4i;
                Unsafe.Add(ref f, a + 1) = s5i + s4r;
                Unsafe.Add(ref f, c) = s5r + s4i;
                Unsafe.Add(ref f, c + 1) = s5i - s4r;
            }
            else
            {
                Unsafe.Add(ref f, a) = s5r + s4i;
                Unsafe.Add(ref f, a + 1) = s5i - s4r;
                Unsafe.Add(ref f, c) = s5r - s4i;
                Unsafe.Add(ref f, c + 1) = s5i + s4r;
            }

            idx++;
        } while (--k != 0);
    }
}