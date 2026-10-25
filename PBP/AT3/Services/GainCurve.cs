using PBP.AT3.Models;

namespace PBP.AT3.Services;

public static class GainCurve
{
    private static readonly ushort[] DeBruijn = [ 0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30, 8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31 ];

    public static ushort GetFirstSetBit(uint x)
    {
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;

        return DeBruijn[unchecked(x * 0x07C4ACDDu) >> 27];
    }

    public static ushort RelationToIdx(float x)
    {
        if (x <= 0.5f)
        {
            x = 1.0f / MathF.Max(x, 0.00048828125f);
            return (ushort)(4u + GetFirstSetBit((uint)x));
        }

        x = MathF.Min(x, 16.0f);

        return (ushort)(4u - GetFirstSetBit((uint)x));
    }

    private static float CalculateRms(ReadOnlySpan<float> input)
    {
        float s = 0;

        for (int i = 0; i < input.Length; i++)
            s += input[i] * input[i];

        s /= (uint)input.Length;

        return MathF.Sqrt(s);
    }

    private static float CalculatePeak(ReadOnlySpan<float> input)
    {
        float s = 0;

        for (int i = 0; i < input.Length; i++)
        {
            float absVal = MathF.Abs(input[i]);

            if (absVal > s)
                s = absVal;
        }

        return s;
    }

    public static List<float> AnalyzeGain(ReadOnlySpan<float> input, uint len, uint maxPoints, bool useRms, List<float>? subframeLow = null, List<float>? subframeHigh = null)
    {
        var res = new List<float>();
        uint step = len / maxPoints;

        subframeLow?.Clear();
        subframeHigh?.Clear();

        Span<float> microBuffer = stackalloc float[64];

        for (uint pos = 0; pos < len; pos += step)
        {
            var window = input.Slice((int)pos, (int)step);
            float val = useRms ? CalculateRms(window) : CalculatePeak(window);

            res.Add(val);

            if (subframeLow != null || subframeHigh != null)
            {
                const uint chunks = 8;
                uint chunkSz = Math.Max(1u, step / chunks);
                int chunkCount = (int)((step + chunkSz - 1) / chunkSz);

                Span<float> micro = chunkCount <= 64 ? microBuffer[..chunkCount] : new float[chunkCount];
                int mi = 0;

                for (uint off = 0; off < step; off += chunkSz)
                {
                    uint n = Math.Min(chunkSz, step - off);
                    var chunk = input.Slice((int)(pos + off), (int)n);

                    micro[mi++] = useRms ? CalculateRms(chunk) : CalculatePeak(chunk);
                }

                micro.Sort();

                int loIdx = micro.Length / 4;
                int hiIdx = (micro.Length * 3) / 4;

                subframeLow?.Add(micro[loIdx]);
                subframeHigh?.Add(micro[hiIdx]);
            }
        }

        return res;
    }

    [ThreadStatic]
    private static double[]? _energyScratch;

    public static List<float> AnalyzeGainOverlappingRms(ReadOnlySpan<float> input, uint totalLen, uint regionOffset, uint regionLen, uint maxPoints, uint windowLen)
    {
        var result = new List<float>();

        if (totalLen == 0 || regionLen == 0 || maxPoints == 0 || regionOffset >= totalLen)
            return result;

        uint step = regionLen / maxPoints;

        if (step == 0)
            return result;

        uint effectiveWindow = Math.Max(1u, windowLen);
        var accumulatedEnergy = _energyScratch;

        if (accumulatedEnergy == null || accumulatedEnergy.Length < (long)totalLen + 1)
            _energyScratch = accumulatedEnergy = new double[(long)totalLen + 1];

        accumulatedEnergy[0] = 0.0;

        for (uint i = 0; i < totalLen; ++i)
            accumulatedEnergy[i + 1] = accumulatedEnergy[i] + (double)input[(int)i] * input[(int)i];

        for (uint point = 0; point < maxPoints; ++point)
        {
            ulong centre64 = (ulong)regionOffset + (ulong)point * step + step / 2;
            uint centre = (uint)Math.Min(centre64, (ulong)totalLen);
            long start = (long)centre - (long)(effectiveWindow / 2);
            long end = start + effectiveWindow;

            start = Math.Max(0L, start);
            end = Math.Min((long)totalLen, end);

            if (end <= start)
            {
                result.Add(0.0f);
                continue;
            }

            double energy = accumulatedEnergy[end] - accumulatedEnergy[start];

            result.Add((float)Math.Sqrt(energy / (double)(end - start)));
        }
        return result;
    }

    private static void MedianFilter1(List<float> input, float[] output)
    {
        int n = input.Count;
        Span<float> w = stackalloc float[3];

        for (int i = 0; i < n; ++i)
        {
            int lo = Math.Max(0, i - 1);
            int hi = Math.Min(n - 1, i + 1);
            int wn = 0;

            for (int j = lo; j <= hi; ++j)
                w[wn++] = input[j];

            w[..wn].Sort();

            output[i] = w[wn / 2];
        }
    }

    private readonly struct PlateauResult(float level, float maxRaw, bool releaseAtEnd)
    {
        public readonly float Level = level;

        public readonly float MaxRaw = maxRaw;

        public readonly bool ReleaseAtEnd = releaseAtEnd;
    }

    private static PlateauResult FindPlateau(List<float> input, int minContiguous)
    {
        int n = input.Count;
        float maxRaw = 0.0f;

        for (int i = 0; i < n; ++i)
            maxRaw = MathF.Max(maxRaw, input[i]);

        if (n < minContiguous)
            return new PlateauResult(0.0f, maxRaw, false);

        var filtered = new float[n];

        MedianFilter1(input, filtered);

        float bestLevel = 0.0f;
        int bestEnd = -1;

        for (int j = 0; j + minContiguous <= n; ++j)
        {
            float minVal = filtered[j];

            for (int k = 1; k < minContiguous; ++k)
                minVal = MathF.Min(minVal, filtered[j + k]);

            if (minVal > bestLevel)
            {
                bestLevel = minVal;
                bestEnd = j + minContiguous - 1;
            }
        }

        if (bestLevel < 1e-6f)
            return new PlateauResult(0.0f, maxRaw, false);

        while (bestEnd + 1 < n && filtered[bestEnd + 1] >= bestLevel)
            ++bestEnd;

        const float hardTailRatio = 0.1f;
        const float releaseHighTol = 0.7f;
        const float releaseTailRatio = 0.5f;

        bool releaseAtEnd = false;

        if (bestEnd < n - 1)
        {
            if (input[n - 1] < bestLevel * hardTailRatio)
                releaseAtEnd = true;
            else
            {
                bool anyHighAfter = false;

                for (int i = bestEnd + 1; i < n; ++i)
                {
                    if (input[i] >= bestLevel * releaseHighTol)
                    {
                        anyHighAfter = true;
                        break;
                    }
                }

                releaseAtEnd = !anyHighAfter && (input[n - 1] < bestLevel * releaseTailRatio);
            }
        }

        return new PlateauResult(bestLevel, maxRaw, releaseAtEnd);
    }

    private static float BoundaryTransientScore(float[] env, int loc, int win)
    {
        int n = env.Length;
        int leftStart = Math.Max(0, loc - win);
        int leftEnd = loc;
        int rightStart = loc;
        int rightEnd = Math.Min(n, loc + win);
        float leftMax = 0.0f;

        for (int i = leftStart; i < leftEnd; ++i)
            leftMax = MathF.Max(leftMax, env[i]);

        float rightMax = 0.0f;

        for (int i = rightStart; i < rightEnd; ++i)
            rightMax = MathF.Max(rightMax, env[i]);

        const float eps = 1e-9f;

        float attack = (rightMax + eps) / (leftMax + eps);
        float release = (leftMax + eps) / (rightMax + eps);

        return MathF.Max(attack, release);
    }

    private struct CarrierPeriodicity
    {
        public float Score;

        public int Lag;

        public float TrendOctaves;

        public float TurningFraction;
    }

    private static CarrierPeriodicity AnalyzeCarrierPeriodicity(List<float> input)
    {
        var result = new CarrierPeriodicity();
        int n = input.Count;

        if (n < 12)
            return result;

        var logEnv = new float[n];
        float meanX = 0.0f;

        for (int i = 0; i < n; ++i)
        {
            logEnv[i] = MathF.Log2(MathF.Max(input[i], 1e-9f));
            meanX += logEnv[i];
        }

        meanX /= n;

        float meanT = 0.5f * (n - 1);
        float slopeNumerator = 0.0f;
        float slopeDenominator = 0.0f;

        for (int i = 0; i < n; ++i)
        {
            float dt = i - meanT;

            slopeNumerator += dt * (logEnv[i] - meanX);
            slopeDenominator += dt * dt;
        }

        float slope = slopeNumerator / MathF.Max(slopeDenominator, 1e-9f);

        result.TrendOctaves = MathF.Abs(slope) * (n - 1);

        var residual = new float[n];

        for (int i = 0; i < n; ++i)
            residual[i] = logEnv[i] - (meanX + slope * (i - meanT));

        int maxLag = Math.Min(10, n / 3);

        for (int lag = 2; lag <= maxLag; ++lag)
        {
            int count = n - lag;
            float meanA = 0.0f;
            float meanB = 0.0f;

            for (int i = 0; i < count; ++i)
            {
                meanA += residual[i];
                meanB += residual[i + lag];
            }

            meanA /= count;
            meanB /= count;

            float covariance = 0.0f;
            float energyA = 0.0f;
            float energyB = 0.0f;

            for (int i = 0; i < count; ++i)
            {
                float a = residual[i] - meanA;
                float b = residual[i + lag] - meanB;

                covariance += a * b;
                energyA += a * a;
                energyB += b * b;
            }

            float score = covariance / MathF.Sqrt(MathF.Max(energyA * energyB, 1e-18f));

            if (score > result.Score)
            {
                result.Score = score;
                result.Lag = lag;
            }
        }

        int turns = 0;

        for (int i = 1; i + 1 < n; ++i)
        {
            float left = logEnv[i] - logEnv[i - 1];
            float right = logEnv[i + 1] - logEnv[i];

            if (left * right < 0.0f)
                ++turns;
        }

        result.TurningFraction = (float)turns / (n - 2);

        return result;
    }

    private static bool IsCarrierRipple(List<float> input, out CarrierPeriodicity periodicity)
    {
        periodicity = AnalyzeCarrierPeriodicity(input);

        float minScore = periodicity.Lag <= 6 ? 0.45f : 0.70f;

        return periodicity.Lag >= 2 && periodicity.Score >= minScore && periodicity.TrendOctaves < 1.0f && periodicity.TurningFraction >= 0.55f;
    }

    private struct Transition
    {
        public int Loc;

        public ushort Level;

        public int Delta;
    }

    public static List<GainCurvePoint> CalcCurve(List<float> input, CurveBuilderCtx ctx, float minScore, List<float>? subframeLow, List<float>? subframeHigh)
    {
        var curve = new List<GainCurvePoint>();

        if (ctx.CarrierRippleHold > 0)
            --ctx.CarrierRippleHold;

        if (input.Count == 0)
            return curve;

        const int minPlateauLen = 3;

        PlateauResult plateau = FindPlateau(input, minPlateauLen);
        const float minPlateauFraction = 0.4f;
        bool usePlateau = plateau.Level > 1e-6f && !plateau.ReleaseAtEnd && plateau.Level >= plateau.MaxRaw * minPlateauFraction;
        float target = usePlateau ? plateau.Level : input[^1];
        float savedLastLevel = ctx.LastLevel;
        float savedLastTarget = ctx.LastTarget;

        ctx.LastLevel = input[^1];
        ctx.LastTarget = target;

        if (target < 1e-6f)
            return curve;

        if (savedLastLevel < 1e-6f)
            return curve;

        int n = input.Count;
        var filtered = new float[n];

        MedianFilter1(input, filtered);

        float maxGain = 0.0f;

        foreach (float v in input)
            maxGain = MathF.Max(maxGain, v);

        const float stickyMaxIntraFrameRatio = 7.0f;
        const float stickyMaxInterFrameRatio = 10.0f;
        float intraRatio = maxGain / MathF.Max(target, 1e-9f);
        float interRatio = 1.0f;

        if (savedLastTarget > 1e-6f)
        {
            float hi = MathF.Max(savedLastTarget, target);
            float lo = MathF.Min(savedLastTarget, target);

            interRatio = hi / MathF.Max(lo, 1e-9f);
        }

        bool stickyFrameEligible = subframeLow != null && subframeHigh != null && subframeLow.Count == input.Count && subframeHigh.Count == input.Count && intraRatio <= stickyMaxIntraFrameRatio && interRatio <= stickyMaxInterFrameRatio;
        var sfLevel = new ushort[n];

        for (int i = 0; i < n; ++i)
        {
            float ratioCenter = filtered[i] / target;
            ushort level = RelationToIdx(ratioCenter);

            if (i > 0 && stickyFrameEligible)
            {
                float ratioLo = subframeLow![i] / target;
                float ratioHi = subframeHigh![i] / target;

                if (ratioLo > ratioHi)
                    (ratioLo, ratioHi) = (ratioHi, ratioLo);

                ushort idxLo = RelationToIdx(ratioLo);
                ushort idxHi = RelationToIdx(ratioHi);
                ushort minIdx = Math.Min(idxLo, idxHi);
                ushort maxIdx = Math.Max(idxLo, idxHi);
                ushort prev = sfLevel[i - 1];
                ushort idxSpan = (ushort)(maxIdx - minIdx);

                if (idxSpan <= 1u && Math.Abs((int)level - (int)prev) == 1 && prev >= minIdx && prev <= maxIdx)
                    level = prev;
            }

            sfLevel[i] = level;
        }

        int targetSf = 0;

        for (int sf = n - 2; sf >= 0; --sf)
        {
            if (sfLevel[sf] != 4u)
            {
                targetSf = sf + 1;
                break;
            }
        }

        if (targetSf == 0)
            return curve;

        var boundaryScore = new float[n + 1];

        Array.Fill(boundaryScore, 1.0f);

        const int transientScoreWindow = 3;

        for (int loc = 1; loc <= targetSf; ++loc)
            boundaryScore[loc] = BoundaryTransientScore(filtered, loc, transientScoreWindow);

        var trans = new List<Transition>();
        {
            ushort prev = 4;

            for (int sf = targetSf - 1; sf >= 0; --sf)
            {
                ushort lev = sfLevel[sf];

                if (lev != prev)
                {
                    int loc = sf + 1;
                    int delta = Math.Abs((int)lev - (int)prev);
                    float score = boundaryScore[loc];
                    bool keep = (loc == targetSf) || (delta >= 2) || (score >= minScore);

                    if (keep)
                    {
                        trans.Add(new Transition { Loc = loc, Level = lev, Delta = delta });

                        prev = lev;
                    }
                }
            }

            trans.Reverse();
        }

        if (trans.Count == 0)
            return curve;

        bool weakCurve = true;

        foreach (var t in trans)
        {
            if (!(t.Level >= 3u && t.Level <= 5u))
            {
                weakCurve = false;
                break;
            }
        }

        CarrierPeriodicity periodicity = default;

        if (!weakCurve)
            ctx.CarrierRippleHold = 0;

        bool carrierRipple = weakCurve && IsCarrierRipple(input, out periodicity);
        const float strongCarrierScore = 0.80f;
        bool strongCarrierEvidence = carrierRipple && periodicity.Score >= strongCarrierScore;
        bool rejectCarrierRipple = carrierRipple && (ctx.CarrierRippleHold > 0 || strongCarrierEvidence);

        if (carrierRipple)
            ctx.CarrierRippleHold = 8;

        if (rejectCarrierRipple)
            return curve;

        const int maxCurvePoints = 6;

        if (trans.Count > maxCurvePoints)
        {
            trans.Sort(static (a, b) =>
            {
                if (a.Delta != b.Delta)
                    return b.Delta.CompareTo(a.Delta);

                return b.Loc.CompareTo(a.Loc);
            });

            trans.RemoveRange(maxCurvePoints, trans.Count - maxCurvePoints);
            trans.Sort(static (a, b) => a.Loc.CompareTo(b.Loc));
        }

        foreach (var t in trans)
            curve.Add(new GainCurvePoint(t.Level, (uint)t.Loc));

        return curve;
    }
}