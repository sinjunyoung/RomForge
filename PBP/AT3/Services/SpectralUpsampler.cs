using PBP.AT3.Enums;
using PBP.AT3.Models;

namespace PBP.AT3.Services;

public sealed class SpectralUpsampler
{
    public const int InN = 512;

    public const int Upsample = 8;

    public const int OutN = InN * Upsample;

    public const float DefaultEps = 0.15f;

    public const float HighFreqThreshold = 0.05f;

    private static readonly float NaturalToDb = (float)(20.0f / 2.302585092994046);

    private readonly int _lowCutBin;
    private readonly int _minPitchLag;
    private readonly int _maxPitchLag;
    private readonly float[] _win = new float[InN];
    private readonly KissFftReal _fwd = new(InN, false);
    private readonly KissFftReal _cepInv = new(InN, true);
    private readonly KissFftReal _inv = new(OutN, true);
    private readonly float[] _windowed = new float[InN];
    private readonly float[] _fwdOut = new float[2 * (InN / 2 + 1)];
    private readonly float[] _cepIn = new float[2 * (InN / 2 + 1)];
    private readonly float[] _cepstrum = new float[InN];
    private readonly float[] _invIn = new float[2 * (OutN / 2 + 1)];

    public SpectralUpsampler(float sampleRate, float lowCutHz, float epsilon = DefaultEps)
    {
        _lowCutBin = (int)MathF.Ceiling(lowCutHz * InN / sampleRate);
        _minPitchLag = Math.Max(2, (int)MathF.Floor(sampleRate / 1200.0f));
        _maxPitchLag = Math.Min(InN / 2 - 1, (int)MathF.Ceiling(sampleRate / 70.0f));

        float eN = epsilon * (float)InN;
        float fN = (float)InN;

        for (int n = 0; n < InN; ++n)
        {
            float fn = (float)n;

            if (n == 0)
                _win[n] = 0.0f;
            else if (fn < eN)
            {
                float zp = eN * (1.0f / fn + 1.0f / (fn - eN));

                _win[n] = 1.0f / (1.0f + MathF.Exp(zp));
            }
            else if (fn <= fN - eN)
                _win[n] = 1.0f;
            else
            {
                float m = fN - fn;
                float zp = eN * (1.0f / m + 1.0f / (m - eN));

                _win[n] = 1.0f / (1.0f + MathF.Exp(zp));
            }
        }
    }

    private static float Hypot(float x, float y) => (float)Math.Sqrt((double)x * x + (double)y * y);

    private float FilterWeight(int k)
    {
        if (_lowCutBin == 0)
            return 1.0f;

        if (k >= _lowCutBin + 2)
            return 1.0f;

        if (k >= _lowCutBin)
        {
            int i = k - _lowCutBin + 1;
            return 0.5f * (1.0f - MathF.Cos((float)Math.PI * i / 2.0f));
        }

        return 0.0f;
    }

    public static float CalcMagnitudeChangeDb(float[] current, float[] previous)
    {
        if (current.Length == 0 || current.Length != previous.Length)
            return float.PositiveInfinity;

        float peak = 0.0f;
        float maxEnergy = 0.0f;

        for (int i = 0; i < current.Length; ++i)
        {
            peak = MathF.Max(peak, MathF.Max(current[i], previous[i]));
            maxEnergy = MathF.Max(maxEnergy, 0.5f * (current[i] * current[i] + previous[i] * previous[i]));
        }

        if (peak <= 1e-15f || maxEnergy <= 1e-30f)
            return float.PositiveInfinity;

        float floor = peak * 1e-5f;
        float energyThreshold = maxEnergy * 1e-5f;
        double weightedChange = 0.0;
        double weightSum = 0.0;

        for (int i = 0; i < current.Length; ++i)
        {
            double energy = 0.5 * ((double)current[i] * current[i] + (double)previous[i] * previous[i]);

            if (energy < energyThreshold)
                continue;

            double change = Math.Abs(20.0 * Math.Log10(((double)current[i] + floor) / ((double)previous[i] + floor)));

            weightedChange += energy * change;
            weightSum += energy;
        }

        return weightSum > 0.0 ? (float)(weightedChange / weightSum) : float.PositiveInfinity;
    }

    public static HarmonicAliasEvidence CalcHarmonicAliasEvidence(float[] harmonicMagnitude, float[] neighborMagnitude, float pitchPeriod, bool nearNyquist)
    {
        var evidence = new HarmonicAliasEvidence();

        if (harmonicMagnitude.Length != neighborMagnitude.Length || harmonicMagnitude.Length < 129 || !float.IsFinite(pitchPeriod) || pitchPeriod <= 0.0f)
            return evidence;

        const int edgeBins = 64;
        int nyquist = harmonicMagnitude.Length - 1;
        int first = nearNyquist ? nyquist - edgeBins : 1;
        int last = nearNyquist ? nyquist : Math.Min(edgeBins, nyquist);

        if (first >= last)
            return evidence;

        float RangeMedian(float[] values)
        {
            var tmp = new float[last - first + 1];

            Array.Copy(values, first, tmp, 0, tmp.Length);
            Array.Sort(tmp);

            return tmp[tmp.Length / 2];
        }

        float harmonicFloor = RangeMedian(harmonicMagnitude);
        float neighborFloor = RangeMedian(neighborMagnitude);
        float harmonicMax = 0.0f;
        float neighborMax = 0.0f;

        for (int k = first; k <= last; ++k)
        {
            harmonicMax = MathF.Max(harmonicMax, harmonicMagnitude[k]);
            neighborMax = MathF.Max(neighborMax, neighborMagnitude[k]);
        }

        if (harmonicMax <= 1e-15f || neighborMax <= 1e-15f)
            return evidence;

        float harmonicThreshold = MathF.Max(harmonicFloor * 3.0f, harmonicMax * 0.02f);
        float neighborThreshold = MathF.Max(neighborFloor * 3.0f, neighborMax * 0.02f);
        var peaks = new List<int>();

        for (int k = first + 2; k + 2 <= last; ++k)
        {
            if (harmonicMagnitude[k] < harmonicThreshold)
                continue;

            bool localMaximum = true;

            for (int offset = -2; offset <= 2; ++offset)
            {
                if (offset != 0 && harmonicMagnitude[k] < harmonicMagnitude[k + offset])
                {
                    localMaximum = false;
                    break;
                }
            }

            if (localMaximum)
                peaks.Add(k);
        }

        float pitchBins = (float)InN / pitchPeriod;
        var belongsToComb = new bool[peaks.Count];

        for (int i = 0; i < peaks.Count; ++i)
        {
            for (int j = i + 1; j < peaks.Count; ++j)
            {
                float distance = (float)(peaks[j] - peaks[i]);
                float harmonic = MathF.Round(distance / pitchBins, MidpointRounding.AwayFromZero);
                float tolerance = MathF.Max(1.5f, pitchBins * 0.08f);

                if (harmonic >= 1.0f && MathF.Abs(distance - harmonic * pitchBins) <= tolerance)
                {
                    belongsToComb[i] = true;
                    belongsToComb[j] = true;
                }
            }
        }

        double dot = 0.0;
        double harmonicNorm = 0.0;
        double neighborNorm = 0.0;

        for (int k = first; k <= last; ++k)
        {
            float position = (float)(k - first) / (float)(last - first);
            float boundaryPosition = nearNyquist ? position : 1.0f - position;
            float weight = MathF.Sin((float)(Math.PI * 0.5) * boundaryPosition);
            float weighted = weight * weight;
            float a = MathF.Max(0.0f, harmonicMagnitude[k] - harmonicFloor);
            float b = MathF.Max(0.0f, neighborMagnitude[k] - neighborFloor);

            dot += weighted * a * b;
            harmonicNorm += weighted * a * a;
            neighborNorm += weighted * b * b;
        }

        if (harmonicNorm > 0.0 && neighborNorm > 0.0)
            evidence.SpectralCoherence = (float)(dot / Math.Sqrt(harmonicNorm * neighborNorm));

        double totalPeakEnergy = 0.0;
        double matchedPeakEnergy = 0.0;

        for (int i = 0; i < peaks.Count; ++i)
        {
            if (!belongsToComb[i])
                continue;

            ++evidence.HarmonicPeaks;

            int k = peaks[i];
            float peakEnergy = harmonicMagnitude[k] * harmonicMagnitude[k];

            totalPeakEnergy += peakEnergy;

            bool matched = false;
            int matchFirst = k > 2 ? k - 2 : 0;
            int matchLast = Math.Min(k + 2, neighborMagnitude.Length - 1);

            for (int m = matchFirst; m <= matchLast; ++m)
                matched = matched || neighborMagnitude[m] >= neighborThreshold;

            if (matched)
            {
                ++evidence.MatchedPeaks;
                matchedPeakEnergy += peakEnergy;
            }
        }

        if (totalPeakEnergy > 0.0)
            evidence.SharedPeakRatio = (float)(matchedPeakEnergy / totalPeakEnergy);

        return evidence;
    }

    public UpsamplerResult Process(ReadOnlySpan<float> input, SpectrumOrientation orientation = SpectrumOrientation.Direct)
    {
        var windowed = _windowed;

        for (int n = 0; n < InN; ++n)
            windowed[n] = input[n] * _win[n];

        const int inBins = InN / 2 + 1;
        var fwdOut = _fwdOut;

        _fwd.Forward(windowed, fwdOut);

        float maxMagnitude = 0.0f;

        for (int k = 0; k < inBins; ++k)
            maxMagnitude = MathF.Max(maxMagnitude, Hypot(fwdOut[2 * k], fwdOut[2 * k + 1]));

        float magnitudeFloor = MathF.Max(maxMagnitude * 1e-6f, 1e-15f);
        var cepIn = _cepIn;

        for (int k = 0; k < inBins; ++k)
        {
            int sourceBin = orientation == SpectrumOrientation.Inverted ? InN / 2 - k : k;
            float magnitude = Hypot(fwdOut[2 * sourceBin], fwdOut[2 * sourceBin + 1]);

            cepIn[2 * k] = MathF.Log(MathF.Max(magnitude, magnitudeFloor));
            cepIn[2 * k + 1] = 0.0f;
        }

        var cepstrum = _cepstrum;

        _cepInv.Inverse(cepIn, cepstrum);

        float cepNorm = 1.0f / (float)InN;

        for (int i = 0; i < cepstrum.Length; i++)
            cepstrum[i] *= cepNorm;

        float meanQ = 0.0f;
        float meanCep = 0.0f;
        int pitchCount = _maxPitchLag - _minPitchLag + 1;

        for (int q = _minPitchLag; q <= _maxPitchLag; ++q)
        {
            meanQ += q;
            meanCep += cepstrum[q];
        }

        meanQ /= pitchCount;
        meanCep /= pitchCount;

        float slopeNumerator = 0.0f;
        float slopeDenominator = 0.0f;

        for (int q = _minPitchLag; q <= _maxPitchLag; ++q)
        {
            float dq = q - meanQ;

            slopeNumerator += dq * (cepstrum[q] - meanCep);
            slopeDenominator += dq * dq;
        }

        float cepSlope = slopeNumerator / MathF.Max(slopeDenominator, 1e-20f);
        float CepResidual(int q) => cepstrum[q] - (meanCep + cepSlope * (q - meanQ));
        int peakLag = _minPitchLag;
        float peakResidual = CepResidual(peakLag);

        for (int q = _minPitchLag + 1; q <= _maxPitchLag; ++q)
        {
            float residual = CepResidual(q);

            if (residual > peakResidual)
            {
                peakResidual = residual;
                peakLag = q;
            }
        }

        float pitchPeriod = (float)peakLag;

        if (peakLag > _minPitchLag && peakLag < _maxPitchLag)
        {
            float ym = CepResidual(peakLag - 1);
            float y0 = peakResidual;
            float yp = CepResidual(peakLag + 1);
            float denominator = ym - 2.0f * y0 + yp;

            if (MathF.Abs(denominator) > 1e-15f)
                pitchPeriod += 0.5f * (ym - yp) / denominator;
        }

        float cepstralProminenceDb = peakResidual * NaturalToDb;
        double totalE = 0.0;
        double filtHighE = 0.0;
        var rawMagnitude = new float[inBins];
        var filteredMagnitude = new float[inBins];

        for (int k = 0; k <= InN / 2; ++k)
        {
            double e = (double)fwdOut[2 * k] * fwdOut[2 * k] + (double)fwdOut[2 * k + 1] * fwdOut[2 * k + 1];

            totalE += e;

            float h = FilterWeight(k);

            rawMagnitude[k] = MathF.Sqrt((float)e);
            filteredMagnitude[k] = rawMagnitude[k] * h;
            filtHighE += e * h * h;
        }

        float highFreqRatio = (totalE > 0.0) ? (float)(filtHighE / totalE) : 0.0f;
        var invIn = _invIn;

        Array.Clear(invIn);

        float scale = (float)Upsample;
        int passbandStart = (_lowCutBin == 0) ? 0 : _lowCutBin + 2;

        for (int k = passbandStart; k < InN / 2; ++k)
        {
            invIn[2 * k] = fwdOut[2 * k] * scale;
            invIn[2 * k + 1] = fwdOut[2 * k + 1] * scale;
        }

        if (_lowCutBin > 0)
        {
            for (int i = 1; i < 3; ++i)
            {
                int k = _lowCutBin - 1 + i;

                if (k >= InN / 2)
                    continue;

                float w = 0.5f * (1.0f - MathF.Cos((float)Math.PI * i / 2.0f));

                invIn[2 * k] = fwdOut[2 * k] * scale * w;
                invIn[2 * k + 1] = fwdOut[2 * k + 1] * scale * w;
            }
        }

        if (_lowCutBin + 2 <= InN / 2)
        {
            invIn[2 * (InN / 2)] = fwdOut[2 * (InN / 2)] * scale * 0.5f;
            invIn[2 * (InN / 2) + 1] = 0.0f;
        }

        var output = new float[OutN];

        _inv.Inverse(invIn, output);

        float norm = 1.0f / (float)OutN;

        for (int i = 0; i < output.Length; i++)
            output[i] *= norm;

        return new UpsamplerResult
        {
            Signal = output,
            HighFreqRatio = highFreqRatio,
            RawMagnitude = rawMagnitude,
            FilteredMagnitude = filteredMagnitude,
            PitchPeriod = pitchPeriod,
            CepstralProminenceDb = cepstralProminenceDb
        };
    }
}