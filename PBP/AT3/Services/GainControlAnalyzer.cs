using PBP.AT3.Enums;
using PBP.AT3.Models;

namespace PBP.AT3.Services;

public sealed class GainControlAnalyzer
{
    private const float MinScore = 1.9f;
    private const float MinHfrForPitchRms = 0.20f;
    private const float StrongCepstralProminenceDb = 2.0f;
    private const float MinAliasCoherence = 0.45f;
    private const float MinAliasSharedPeakRatio = 0.35f;
    private const uint MinAliasMatchedPeaks = 2;
    private const float MinCepstralProminenceDb = 1.53f;
    private const float MaxPitchJitterCents = 75.0f;
    private const float MaxMagnitudeChangeDb = 7.31f;
    private const float MinContinuousCepstralProminenceDb = 1.25f;
    private const float MaxBoundaryPitchDeltaCents = 100.0f;
    private const float MinSignalThreshold = 1e-4f;
    private const float MinHfrForAmplify = 0.3f;
    private const float Point0WorseTol = 0.02f;
    private const float BoundaryKeepMargin = 0.20f;
    private const float LeakWeight = 0.25f;

    private sealed class HarmonicGainCtx
    {
        public float[] PrevFilteredMagnitude = [];

        public float LastPitchPeriod;

        public float PreviousPitchPeriod;

        public float LastCepstralProminenceDb;

        public float PreviousCepstralProminenceDb;

        public float LastHighFreqRatio;

        public float PreviousHighFreqRatio;

        public byte PitchHistorySize;
    }

    private sealed class BandAnalysis
    {
        public UpsamplerResult Result = null!;

        public List<float> GainLow = [];

        public List<float> GainHigh = [];

        public List<float> ShortGain = [];

        public List<float> PitchGain = [];

        public uint PitchWindow;
    }

    private readonly SpectralUpsampler[][] _upsamplers = NewUpsamplers();
    private readonly BandAnalysis[][] _pending = [new BandAnalysis[4], new BandAnalysis[4]];
    private readonly CurveBuilderCtx[][] _curveCtx = NewCtx();
    private readonly CurveBuilderCtx[][] _pitchCurveCtx = NewCtx();
    private readonly HarmonicGainCtx[][] _harmonicCtx = NewHarmonic();

    private static CurveBuilderCtx[][] NewCtx()
    {
        var a = new CurveBuilderCtx[2][];

        for (int c = 0; c < 2; c++)
        {
            a[c] = new CurveBuilderCtx[4];

            for (int b = 0; b < 4; b++)
                a[c][b] = new CurveBuilderCtx();
        }

        return a;
    }

    private static SpectralUpsampler[][] NewUpsamplers()
    {
        var a = new SpectralUpsampler[2][];

        for (int c = 0; c < 2; c++)
        {
            a[c] = new SpectralUpsampler[4];

            for (int b = 0; b < 4; b++)
                a[c][b] = new SpectralUpsampler(11025.0f, 800.0f);
        }

        return a;
    }

    private static HarmonicGainCtx[][] NewHarmonic()
    {
        var a = new HarmonicGainCtx[2][];

        for (int c = 0; c < 2; c++)
        {
            a[c] = new HarmonicGainCtx[4];

            for (int b = 0; b < 4; b++)
                a[c][b] = new HarmonicGainCtx();
        }

        return a;
    }

    private static float LimitRel(float x) => MathF.Min(MathF.Max(x, CodecInfo.GainLevel[15]), CodecInfo.GainLevel[0]);

    private static void BuildSubframeDivisors(List<GainCurvePoint> pts, Span<float> outDiv)
    {
        Span<float> sampleDiv = stackalloc float[256];

        sampleDiv.Fill(1.0f);

        uint pos = 0;

        for (int i = 0; i < pts.Count; ++i)
        {
            uint lastPos = pts[i].Location << CodecInfo.LocScale;
            float level = CodecInfo.GainLevel[pts[i].Level];
            int incPos = unchecked((int)(((i + 1) < pts.Count ? pts[i + 1].Level : (uint)CodecInfo.ExponentOffset) - pts[i].Level + (uint)CodecInfo.GainInterpolationPosShift));
            float gainInc = CodecInfo.GainInterpolation[incPos];

            for (; pos < lastPos && pos < 256; ++pos)
                sampleDiv[(int)pos] = level;

            for (; pos < lastPos + CodecInfo.LocSz && pos < 256; ++pos)
            {
                sampleDiv[(int)pos] = level;
                level *= gainInc;
            }
        }

        for (int sf = 0; sf < 32; ++sf)
        {
            float sum = 0.0f;

            for (int s = 0; s < 8; ++s)
                sum += sampleDiv[sf * 8 + s];

            outDiv[sf] = sum / 8.0f;
        }
    }

    private static float CalcCurveEarlyMismatchScore(List<float> gain, float target, List<GainCurvePoint> pts)
    {
        if (gain.Count != 32 || target <= 1e-9f)
            return 0.0f;

        Span<float> div = stackalloc float[32];
        BuildSubframeDivisors(pts, div);

        uint maxLoc = 0;

        foreach (var p in pts)
            maxLoc = Math.Max(maxLoc, p.Location);

        uint evalSf = Math.Min(32u, Math.Max(3u, maxLoc + 3));
        const float eps = 1e-9f;
        float fit = 0.0f;

        for (int sf = 0; sf < evalSf; ++sf)
        {
            float mod = gain[sf] / MathF.Max(div[sf], eps);
            float e = MathF.Log2(MathF.Max(mod, eps) / MathF.Max(target, eps));

            fit += e * e;
        }

        fit /= evalSf;

        float leak = 0.0f;
        float wsum = 0.0f;

        for (int sf = 0; sf + 1 < evalSf; ++sf)
        {
            float a = MathF.Log2(MathF.Max(div[sf], eps));
            float b = MathF.Log2(MathF.Max(div[sf + 1], eps));
            float d = b - a;
            float w = 0.5f * (gain[sf] + gain[sf + 1]);

            leak += d * d * w;
            wsum += w;
        }

        if (wsum > eps)
            leak /= wsum;

        return fit + LeakWeight * leak;
    }

    private static float OctaveFoldedCents(float a, float b)
    {
        float raw = 1200.0f * MathF.Log2(a / b);

        return MathF.Abs(raw - 1200.0f * MathF.Round(raw / 1200.0f, MidpointRounding.AwayFromZero));
    }

    private static float UpdateHpfOverlap(List<float> envelope, CurveBuilderCtx ctx)
    {
        float current = 0.0f;

        foreach (float value in envelope)
            current += value;

        current /= (float)envelope.Count;

        float previous = ctx.LastHpfEnergy;

        ctx.LastHpfEnergy = current;

        return (current > 1e-9f && previous > 1e-9f) ? previous / current : 1.0f;
    }

    private static float BoundaryRms(ReadOnlySpan<float> samples)
    {
        double energy = 0.0;

        for (int i = 0; i < samples.Length; ++i)
            energy += (double)samples[i] * samples[i];

        return (float)Math.Sqrt(energy / samples.Length);
    }

    private static bool IsAliasLinked(HarmonicAliasEvidence e) => e.SpectralCoherence >= MinAliasCoherence && e.SharedPeakRatio >= MinAliasSharedPeakRatio && e.MatchedPeaks >= MinAliasMatchedPeaks;

    private BandAnalysis AnalyzeBand(int channel, int band, float[] input)
    {
        var orientation = (band == 1 || band == 3) ? SpectrumOrientation.Inverted : SpectrumOrientation.Direct;
        var result = _upsamplers[channel][band].Process(input.AsSpan(0, SpectralUpsampler.InN), orientation);
        var analysis = new BandAnalysis { Result = result };

        if (result.HighFreqRatio < SpectralUpsampler.HighFreqThreshold)
            return analysis;

        analysis.ShortGain = GainCurve.AnalyzeGain(result.Signal.AsSpan(1024), 2048, 32, true, analysis.GainLow, analysis.GainHigh);
        analysis.PitchWindow = (uint)Math.Max(64L, (long)MathF.Round(result.PitchPeriod * SpectralUpsampler.Upsample, MidpointRounding.AwayFromZero));
        analysis.PitchGain = GainCurve.AnalyzeGainOverlappingRms(result.Signal, (uint)result.Signal.Length, 1024, 2048, 32, analysis.PitchWindow);

        return analysis;
    }

    public void Analyze(float[][][] upInput, int channelCount)
    {
        int total = channelCount * 4;

        if (total > 1 && Environment.ProcessorCount > 1)
        {
            Parallel.For(0, total, i => _pending[i >> 2][i & 3] = AnalyzeBand(i >> 2, i & 3, upInput[i >> 2][i & 3]));
            return;
        }

        for (int i = 0; i < total; i++)
            _pending[i >> 2][i & 3] = AnalyzeBand(i >> 2, i & 3, upInput[i >> 2][i & 3]);
    }

    public void CreateSubbandInfo(float[][] upInput, int channel, SubbandInfo subbandInfo)
    {
        for (int band = 0; band < 4; ++band)
            _pending[channel][band] = AnalyzeBand(channel, band, upInput[band]);

        CreateSubbandInfo(channel, subbandInfo);
    }

    public void CreateSubbandInfo(int channel, SubbandInfo subbandInfo)
    {
        var pending = _pending[channel];
        var analysis = new UpsamplerResult[4];

        for (int band = 0; band < 4; ++band)
            analysis[band] = pending[band].Result;

        var strongHarmonicBand = new bool[4];

        for (int band = 0; band < 4; ++band)
            strongHarmonicBand[band] = analysis[band].HighFreqRatio >= MinHfrForPitchRms && analysis[band].CepstralProminenceDb >= StrongCepstralProminenceDb;

        var harmonicVeto = (bool[])strongHarmonicBand.Clone();

        for (int boundary = 0; boundary < 3; ++boundary)
        {
            int lowerBand = boundary;
            int upperBand = boundary + 1;
            bool nearNyquist = (boundary & 1) == 0;

            if (strongHarmonicBand[lowerBand])
            {
                var evidence = SpectralUpsampler.CalcHarmonicAliasEvidence(analysis[lowerBand].RawMagnitude, analysis[upperBand].RawMagnitude, analysis[lowerBand].PitchPeriod, nearNyquist);

                if (IsAliasLinked(evidence))
                    harmonicVeto[upperBand] = true;
            }

            if (strongHarmonicBand[upperBand])
            {
                var evidence = SpectralUpsampler.CalcHarmonicAliasEvidence(analysis[upperBand].RawMagnitude, analysis[lowerBand].RawMagnitude, analysis[upperBand].PitchPeriod, nearNyquist);

                if (IsAliasLinked(evidence))
                    harmonicVeto[lowerBand] = true;
            }
        }

        for (int band = 0; band < 4; ++band)
        {
            var result = analysis[band];

            if (result.HighFreqRatio < SpectralUpsampler.HighFreqThreshold)
            {
                _curveCtx[channel][band].LastLevel = 0.0f;
                _curveCtx[channel][band].CarrierRippleHold = 0;
                _pitchCurveCtx[channel][band].LastLevel = 0.0f;
                _pitchCurveCtx[channel][band].CarrierRippleHold = 0;

                var resetCtx = _harmonicCtx[channel][band];

                resetCtx.PrevFilteredMagnitude = [];
                resetCtx.LastPitchPeriod = 0.0f;
                resetCtx.PreviousPitchPeriod = 0.0f;
                resetCtx.LastCepstralProminenceDb = 0.0f;
                resetCtx.PreviousCepstralProminenceDb = 0.0f;
                resetCtx.LastHighFreqRatio = 0.0f;
                resetCtx.PreviousHighFreqRatio = 0.0f;
                resetCtx.PitchHistorySize = 0;
                continue;
            }

            var gainLow = pending[band].GainLow;
            var gainHigh = pending[band].GainHigh;
            var shortGain = pending[band].ShortGain;
            var harmonicCtx = _harmonicCtx[channel][band];
            float previousFrameCepstralProminenceDb = harmonicCtx.LastCepstralProminenceDb;
            float previousFrameHighFreqRatio = harmonicCtx.LastHighFreqRatio;
            float magnitudeChangeDb = SpectralUpsampler.CalcMagnitudeChangeDb(result.FilteredMagnitude, harmonicCtx.PrevFilteredMagnitude);
            float pitchJitterCents = float.PositiveInfinity;

            if (harmonicCtx.PitchHistorySize >= 2 && result.PitchPeriod > 0.0f && harmonicCtx.LastPitchPeriod > 0.0f && harmonicCtx.PreviousPitchPeriod > 0.0f)
                pitchJitterCents = 0.5f * (OctaveFoldedCents(result.PitchPeriod, harmonicCtx.LastPitchPeriod) + OctaveFoldedCents(harmonicCtx.LastPitchPeriod, harmonicCtx.PreviousPitchPeriod));

            bool useStationaryPitchRms = band < 3 && result.HighFreqRatio >= MinHfrForPitchRms && result.CepstralProminenceDb >= MinCepstralProminenceDb && pitchJitterCents <= MaxPitchJitterCents && magnitudeChangeDb <= MaxMagnitudeChangeDb;
            float boundaryPitchDeltaCents = float.PositiveInfinity;

            if (result.PitchPeriod > 0.0f && harmonicCtx.LastPitchPeriod > 0.0f)
                boundaryPitchDeltaCents = OctaveFoldedCents(result.PitchPeriod, harmonicCtx.LastPitchPeriod);

            float confirmedPitchDeltaCents = float.PositiveInfinity;

            if (result.PitchPeriod > 0.0f && harmonicCtx.PreviousPitchPeriod > 0.0f)
                confirmedPitchDeltaCents = OctaveFoldedCents(result.PitchPeriod, harmonicCtx.PreviousPitchPeriod);

            bool hasContinuousPitch = band < 3 && result.HighFreqRatio >= MinHfrForPitchRms && previousFrameHighFreqRatio >= MinHfrForPitchRms && result.CepstralProminenceDb >= MinContinuousCepstralProminenceDb && 
                previousFrameCepstralProminenceDb >= MinContinuousCepstralProminenceDb && boundaryPitchDeltaCents <= MaxBoundaryPitchDeltaCents;
            bool hasConfirmedPitch = hasContinuousPitch && harmonicCtx.PitchHistorySize >= 2 && harmonicCtx.PreviousHighFreqRatio >= MinHfrForPitchRms && harmonicCtx.PreviousCepstralProminenceDb >= MinContinuousCepstralProminenceDb && confirmedPitchDeltaCents <= MaxBoundaryPitchDeltaCents;
            bool usePitchRms = useStationaryPitchRms || hasContinuousPitch;
            bool usePitchBoundaryRms = hasContinuousPitch;
            uint pitchWindow = pending[band].PitchWindow;
            var pitchGain = pending[band].PitchGain;
            const uint boundarySample = 128 * SpectralUpsampler.Upsample;
            uint planckFlatStartQmf = (uint)(SpectralUpsampler.DefaultEps * SpectralUpsampler.InN + 0.999f);
            uint maxBoundaryWindow = (128 - planckFlatStartQmf) * SpectralUpsampler.Upsample;
            uint boundaryWindow = Math.Min(pitchWindow, maxBoundaryWindow);
            float previousBoundaryRms = 0.0f;
            float currentBoundaryRms = 0.0f;

            if (usePitchBoundaryRms && boundaryWindow > 0)
            {
                previousBoundaryRms = BoundaryRms(result.Signal.AsSpan((int)(boundarySample - boundaryWindow), (int)boundaryWindow));
                currentBoundaryRms = BoundaryRms(result.Signal.AsSpan((int)boundarySample, (int)boundaryWindow));
            }

            harmonicCtx.PreviousPitchPeriod = harmonicCtx.LastPitchPeriod;
            harmonicCtx.LastPitchPeriod = result.PitchPeriod;
            harmonicCtx.PreviousCepstralProminenceDb = harmonicCtx.LastCepstralProminenceDb;
            harmonicCtx.LastCepstralProminenceDb = result.CepstralProminenceDb;
            harmonicCtx.PreviousHighFreqRatio = harmonicCtx.LastHighFreqRatio;
            harmonicCtx.LastHighFreqRatio = result.HighFreqRatio;
            harmonicCtx.PitchHistorySize = (byte)Math.Min(2, harmonicCtx.PitchHistorySize + 1);
            harmonicCtx.PrevFilteredMagnitude = result.FilteredMagnitude;

            float shortHpfOverlapRatio = UpdateHpfOverlap(shortGain, _curveCtx[channel][band]);
            float pitchHpfOverlapRatio = UpdateHpfOverlap(pitchGain, _pitchCurveCtx[channel][band]);
            float shortDynamicMinScore = MinScore * MathF.Min(1.5f, MathF.Max(1.0f, shortHpfOverlapRatio));
            float pitchDynamicMinScore = MinScore * MathF.Min(1.5f, MathF.Max(1.0f, pitchHpfOverlapRatio));
            var gain = usePitchRms ? pitchGain : shortGain;
            float prevShortTarget = _curveCtx[channel][band].LastTarget;
            float prevPitchTarget = _pitchCurveCtx[channel][band].LastTarget;
            List<GainCurvePoint> curvePoints;

            if (usePitchRms)
            {
                GainCurve.CalcCurve(shortGain, _curveCtx[channel][band], shortDynamicMinScore, gainLow, gainHigh);

                curvePoints = GainCurve.CalcCurve(pitchGain, _pitchCurveCtx[channel][band], pitchDynamicMinScore, null, null);
            }
            else
            {
                GainCurve.CalcCurve(pitchGain, _pitchCurveCtx[channel][band], pitchDynamicMinScore, null, null);

                curvePoints = GainCurve.CalcCurve(shortGain, _curveCtx[channel][band], shortDynamicMinScore, gainLow, gainHigh);
            }

            float prevTarget = usePitchRms ? prevPitchTarget : prevShortTarget;
            float curTarget = usePitchRms ? _pitchCurveCtx[channel][band].LastTarget : _curveCtx[channel][band].LastTarget;
            bool strongHarmonicCurve = band < 3 && harmonicVeto[band] && curvePoints.Count != 0;

            if ((hasConfirmedPitch || strongHarmonicCurve) && curvePoints.Count != 0)
                curvePoints.Clear();

            if (curvePoints.Count == 0)
                continue;

            float maxGain = 0.0f;

            foreach (float g in gain)
                maxGain = MathF.Max(maxGain, g);

            if (maxGain < MinSignalThreshold)
                curvePoints.Clear();

            if (result.HighFreqRatio < MinHfrForAmplify)
                curvePoints.Clear();

            if (band >= 3)
                curvePoints.Clear();

            if (band < 3)
            {
                var curveBeforePoint0 = new List<GainCurvePoint>(curvePoints);
                bool point0Changed = false;
                float point0PrevReference = prevTarget;
                float hpfRmsNextMod = 0.0f;
                bool hpfRmsNextModValid = false;
                bool pitchBoundaryValid = usePitchBoundaryRms && previousBoundaryRms > 1e-6f && currentBoundaryRms > 1e-6f;

                if (pitchBoundaryValid)
                {
                    float divisor = curvePoints.Count == 0 ? 1.0f : CodecInfo.GainLevel[curvePoints[0].Level];

                    point0PrevReference = previousBoundaryRms;
                    hpfRmsNextMod = currentBoundaryRms / divisor;
                    hpfRmsNextModValid = true;
                }
                else if (curvePoints.Count != 0 && curvePoints[0].Location > 0)
                {
                    uint nBefore = curvePoints[0].Location;
                    float divisor = CodecInfo.GainLevel[curvePoints[0].Level];
                    float sum = 0.0f;

                    for (int sf = 0; sf < nBefore; ++sf)
                        sum += gain[sf];

                    hpfRmsNextMod = (sum / nBefore) / divisor;
                    hpfRmsNextModValid = true;
                }
                else if (curvePoints.Count == 0)
                {
                    float sum = 0.0f;

                    foreach (float v in gain)
                        sum += v;

                    hpfRmsNextMod = sum / gain.Count;
                    hpfRmsNextModValid = true;
                }

                if (hpfRmsNextModValid && point0PrevReference > 1e-6f && hpfRmsNextMod > 1e-6f)
                {
                    ushort point0Level = GainCurve.RelationToIdx(point0PrevReference / hpfRmsNextMod);
                    int idx = curvePoints.FindIndex(p => p.Location == 0);

                    if (idx >= 0)
                    {
                        if (curvePoints[idx].Level != point0Level)
                        {
                            var p = curvePoints[idx];

                            p.Level = point0Level;
                            curvePoints[idx] = p;
                            point0Changed = true;
                        }
                    }
                    else if (point0Level != 4 || curvePoints.Count != 0)
                    {
                        curvePoints.Insert(0, new GainCurvePoint(point0Level, 0));
                        point0Changed = true;
                    }
                }

                if (point0Changed)
                {
                    float scoreBefore = CalcCurveEarlyMismatchScore(gain, curTarget, curveBeforePoint0);
                    float scoreAfter = CalcCurveEarlyMismatchScore(gain, curTarget, curvePoints);
                    bool keepByBoundary = false;

                    if (hpfRmsNextModValid && point0PrevReference > 1e-6f && hpfRmsNextMod > 1e-6f)
                    {
                        static ushort FirstLevel(List<GainCurvePoint> pts) => pts.Count == 0 ? (ushort)CodecInfo.ExponentOffset : (ushort)pts[0].Level;
                        float desiredScale = LimitRel(point0PrevReference / hpfRmsNextMod);
                        float scaleBefore = CodecInfo.GainLevel[FirstLevel(curveBeforePoint0)];
                        float scaleAfter = CodecInfo.GainLevel[FirstLevel(curvePoints)];
                        const float eps = 1e-9f;
                        float boundaryErrBefore = MathF.Abs(MathF.Log2(MathF.Max(scaleBefore, eps) / MathF.Max(desiredScale, eps)));
                        float boundaryErrAfter = MathF.Abs(MathF.Log2(MathF.Max(scaleAfter, eps) / MathF.Max(desiredScale, eps)));

                        keepByBoundary = (boundaryErrAfter + BoundaryKeepMargin < boundaryErrBefore);
                    }

                    if (!keepByBoundary && scoreAfter > scoreBefore * (1.0f + Point0WorseTol))
                        curvePoints = curveBeforePoint0;
                }
            }

            if (curvePoints.Count >= 2 && curvePoints[0].Location == 0 && curvePoints[0].Level == curvePoints[1].Level)
                curvePoints.RemoveAt(0);

            var curve = new List<GainPoint>(curvePoints.Count);

            foreach (var p in curvePoints)
                curve.Add(new GainPoint(p.Level, p.Location));

            subbandInfo.Info[band] = curve;
        }
    }
}