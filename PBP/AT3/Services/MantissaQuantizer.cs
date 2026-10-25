namespace PBP.AT3.Services;

public static class MantissaQuantizer
{
    public static int ToInt(float x) => (int)MathF.Round(x);

    public static float QuantMantisas(ReadOnlySpan<float> input, int first, int last, float mul, bool ea, Span<int> mantisas)
    {
        float e1 = 0.0f;
        float e2 = 0.0f;

        float inv2 = (float)(1.0 / (double)(mul * mul));

        if (!ea)
        {
            for (int j = 0, f = first; f < last; f++, j++)
            {
                float t = input[j] * mul;

                e1 += input[j] * input[j];
                mantisas[f] = ToInt(t);
                e2 += mantisas[f] * mantisas[f] * inv2;
            }
            return e1 / e2;
        }

        var candidates = new List<(float Delta, int F)>(last - first);

        for (int j = 0, f = first; f < last; f++, j++)
        {
            float t = input[j] * mul;

            e1 += input[j] * input[j];
            mantisas[f] = ToInt(t);
            e2 += mantisas[f] * mantisas[f] * inv2;

            float delta = t - (MathF.Truncate(t) + 0.5f);

            if (MathF.Abs(delta) < 0.25f)
                candidates.Add((delta, f));
        }

        if (candidates.Count == 0)
            return e1 / e2;

        candidates.Sort(static (a, b) =>
        {
            int c = MathF.Abs(a.Delta).CompareTo(MathF.Abs(b.Delta));
            return c != 0 ? c : a.F.CompareTo(b.F);
        });

        if (e2 < e1)
        {
            foreach (var (Delta, F) in candidates)
            {
                int f = F;
                int j = f - first;
                float t = input[j] * mul;

                if ((float)Math.Abs(mantisas[f]) < MathF.Abs(t) && (float)Math.Abs(mantisas[f]) < (mul - 1))
                {
                    int m = mantisas[f];

                    if (m > 0)
                        m++;

                    if (m < 0) 
                        m--;

                    if (m == 0) 
                        m = t > 0 ? 1 : -1;

                    float ex = e2;

                    ex -= mantisas[f] * mantisas[f] * inv2;
                    ex += m * m * inv2;

                    if (MathF.Abs(ex - e1) < MathF.Abs(e2 - e1))
                    {
                        mantisas[f] = m;
                        e2 = ex;
                    }
                }
            }

            return e1 / e2;
        }

        if (e2 > e1)
        {
            foreach (var (Delta, F) in candidates)
            {
                int f = F;
                int j = f - first;
                float t = input[j] * mul;

                if ((float)Math.Abs(mantisas[f]) > MathF.Abs(t))
                {
                    int m = mantisas[f];

                    if (m > 0)
                        m--;

                    if (m < 0)
                        m++;

                    float ex = e2;

                    ex -= mantisas[f] * mantisas[f] * inv2;
                    ex += m * m * inv2;

                    if (MathF.Abs(ex - e1) < MathF.Abs(e2 - e1))
                    {
                        mantisas[f] = m;
                        e2 = ex;
                    }
                }
            }

            return e1 / e2;
        }

        return e1 / e2;
    }
}