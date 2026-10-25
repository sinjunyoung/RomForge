using PSP.PBP.AT3.Enums;
using PSP.PBP.AT3.IO;
using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

public sealed class Encoder
{
    private static readonly float LoudFactor = (float)0.006;

    private readonly At3BitStreamWriter _bitStreamWriter;
    private readonly ContainerParams _containerParams;
    private readonly int _channels;
    private readonly float[] _loudnessCurve = PsyCommon.CreateLoudnessCurve(CodecInfo.NumSamples);

    private readonly float[][] _pcmBuffer = NewArrays(8, 512);
    private readonly float[][] _prevPeak = NewArrays(2, 4);
    private readonly AnalysisFilterBank[] _analysisFilterBank = [new(), new()];
    private readonly Mdct _mdct = new(512, 1.0f);
    private readonly List<SingleChannelElement> _elements;
    private readonly float[][] _prevOverlapGainScale = NewArrays(2, 4, 1.0f);
    private readonly float[][][] _lookAheadBuf = [NewArrays(4, 640), NewArrays(4, 640)];

    private readonly bool _noTonalComponents;
    private readonly GainControlAnalyzer? _gainControl;
    private bool _lookAheadPending = true;
    private float _loudness = LoudFactor;

    public Encoder(Writer container, ContainerParams containerParams, int channels, EncoderSettings settings)
    {
        if (channels != 1 && channels != 2)
            throw new ArgumentException("Only mono and stereo are supported", nameof(channels));

        _noTonalComponents = settings.NoTonalComponents;

        if (!settings.NoGainControl)
            _gainControl = new GainControlAnalyzer();

        _containerParams = containerParams;
        _channels = channels;
        _bitStreamWriter = new At3BitStreamWriter(container, containerParams, settings.BfuIdxConst);
        _elements = [];

        for (int i = 0; i < channels; i++)
            _elements.Add(new SingleChannelElement());
    }

    private static float[][] NewArrays(int count, int size, float fill = 0.0f)
    {
        var a = new float[count][];

        for (int i = 0; i < count; i++)
        {
            a[i] = new float[size];

            if (fill != 0.0f)
                Array.Fill(a[i], fill);
        }

        return a;
    }

    private void Matrixing()
    {
        for (int subband = 0; subband < 4; subband++)
        {
            float[] p0 = _pcmBuffer[subband * 2];
            float[] p1 = _pcmBuffer[subband * 2 + 1];

            for (int sample = 0; sample < 256; sample++)
            {
                float t0 = p0[256 + sample];
                float t1 = p1[256 + sample];

                p0[256 + sample] = (float)((t0 + t1) / 2.0);
                p1[256 + sample] = (float)((t0 - t1) / 2.0);
            }
        }
    }

    private static void Modulate(List<GainPoint> giCur, Span<float> bufCur, float[] bufNext, int nextOffset)
    {
        uint pos = 0;
        float scale = CodecInfo.GainLevel[giCur[0].Level];

        for (int i = 0; i < giCur.Count; ++i)
        {
            uint lastPos = giCur[i].Location << CodecInfo.LocScale;
            float level = CodecInfo.GainLevel[giCur[i].Level];
            int incPos = unchecked((int)(((i + 1) < giCur.Count ? giCur[i + 1].Level : (uint)CodecInfo.ExponentOffset) - giCur[i].Level + (uint)CodecInfo.GainInterpolationPosShift));
            float gainInc = CodecInfo.GainInterpolation[incPos];

            for (; pos < lastPos; pos++)
            {
                bufCur[(int)pos] /= scale;
                bufNext[nextOffset + (int)pos] /= level;
            }

            for (; pos < lastPos + CodecInfo.LocSz; pos++)
            {
                bufCur[(int)pos] /= scale;
                bufNext[nextOffset + (int)pos] /= level;
                level *= gainInc;
            }
        }

        for (; pos < CodecInfo.MdctSize / 2; pos++)
            bufCur[(int)pos] /= scale;
    }

    private void MdctBands(float[] specs, int channel, float[] maxLevels, SubbandInfo subbandInfo)
    {
        Span<float> tmp = stackalloc float[512];
        Span<float> sp = stackalloc float[256];

        for (int band = 0; band < 4; ++band)
        {
            float[] srcBuff = _pcmBuffer[channel + band * 2];
            int specOffset = band * 256;

            srcBuff.AsSpan(0, 256).CopyTo(tmp);

            if (band < subbandInfo.GetQmfNum())
            {
                var points = subbandInfo.GetGainPoints(band);

                if (points.Count != 0)
                    Modulate(points, tmp, srcBuff, 256);
            }

            float max = 0.0f;

            for (int i = 0; i < 256; i++)
            {
                max = MathF.Max(max, MathF.Abs(srcBuff[256 + i]));
                srcBuff[i] = CodecInfo.EncodeWindow[i] * srcBuff[256 + i];
                tmp[256 + i] = CodecInfo.EncodeWindow[255 - i] * srcBuff[256 + i];
            }

            _mdct.Transform(tmp, sp);
            sp.CopyTo(specs.AsSpan(specOffset, 256));

            if ((band & 1) != 0)
                specs.AsSpan(specOffset, 256).Reverse();

            maxLevels[band] = max;
        }
    }

    private static List<TonalVal> ExtractTonalComponents(float[] specs, float[] flatnessPerBfu)
    {
        var res = new List<TonalVal>();
        const float flatnessThreshold = 0.01f;
        const int maxTonalLen = 5;

        for (int blockNum = 8; blockNum < 29; ++blockNum)
        {
            if (blockNum >= flatnessPerBfu.Length)
                break;

            float flatness = flatnessPerBfu[blockNum];

            if (flatness >= flatnessThreshold)
                continue;

            int specNumStart = CodecInfo.BlockSizeTab[blockNum];
            int blockLen = CodecInfo.SpecsPerBlock[blockNum];
            int specNumEnd = specNumStart + blockLen;

            if (specNumStart >= specNumEnd)
                continue;

            int maxLen = Math.Min(maxTonalLen, blockLen);
            float bestScore = -1.0f;
            int bestStart = specNumStart;
            int bestLen = 1;

            for (int start = specNumStart; start < specNumEnd; ++start)
            {
                int maxLenForStart = Math.Min(maxLen, specNumEnd - start);
                float score = 0.0f;

                for (int len = 1; len <= maxLenForStart; ++len)
                {
                    score += MathF.Abs(specs[start + len - 1]);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestStart = start;
                        bestLen = len;
                    }
                }
            }

            if (bestScore <= 0.0f)
                continue;

            for (int n = 0; n < bestLen; ++n)
            {
                int pos = bestStart + n;

                res.Add(new TonalVal((ushort)pos, specs[pos], (byte)blockNum));

                specs[pos] = 0.0f;
            }
        }

        return res;
    }

    private static void MapTonalComponents(List<TonalVal> tonalComponents, List<TonalBlock> componentMap)
    {
        Span<float> tmp = stackalloc float[8];

        for (int i = 0; i < tonalComponents.Count;)
        {
            int startPos = i;
            int curPos;

            do
            {
                curPos = tonalComponents[i].Pos;
                ++i;
            } while (i < tonalComponents.Count && tonalComponents[i].Pos == curPos + 1 && i - startPos < 7);

            int len = i - startPos;

            for (int j = 0; j < len; ++j)
                tmp[j] = (float)tonalComponents[startPos + j].Val;

            var scaledBlock = Scaler.Scale(tmp[..len]);

            componentMap.Add(new TonalBlock { Val = tonalComponents[startPos], ScaledBlock = scaledBlock });
        }
    }

    private static float SafeEnergyScale(float originalEnergy, float modulatedEnergy)
    {
        const float eps = 1.0e-20f;

        if (originalEnergy <= eps || modulatedEnergy <= eps || !float.IsFinite(originalEnergy) || !float.IsFinite(modulatedEnergy))
            return 1.0f;

        float scale = originalEnergy / modulatedEnergy;

        return float.IsFinite(scale) && scale > 0.0f ? scale : 1.0f;
    }

    private static void BuildSampleDivisors(IReadOnlyList<GainPoint> pts, Span<float> outDiv)
    {
        outDiv.Fill(1.0f);

        uint pos = 0;

        for (int i = 0; i < pts.Count; ++i)
        {
            uint lastPos = pts[i].Location << CodecInfo.LocScale;
            float level = CodecInfo.GainLevel[pts[i].Level];
            int incPos = unchecked((int)(((i + 1) < pts.Count ? pts[i + 1].Level : (uint)CodecInfo.ExponentOffset) - pts[i].Level + (uint)CodecInfo.GainInterpolationPosShift));
            float gainInc = CodecInfo.GainInterpolation[incPos];

            for (; pos < lastPos && pos < 256; ++pos)
                outDiv[(int)pos] = level;

            for (; pos < lastPos + CodecInfo.LocSz && pos < 256; ++pos)
            {
                outDiv[(int)pos] = level;
                level *= gainInc;
            }
        }
    }

    public static GainEnergyAnalysis CalcGainEnergyScale(ReadOnlySpan<float> prevOverlap, ReadOnlySpan<float> curInput, IReadOnlyList<GainPoint> gainPoints, float prevOverlapScale)
    {
        if (!float.IsFinite(prevOverlapScale) || prevOverlapScale <= 0.0f)
            prevOverlapScale = 1.0f;

        float prevDiv = gainPoints.Count == 0 ? 1.0f : CodecInfo.GainLevel[gainPoints[0].Level];
        float prevStoredEnergy = 0.0f;

        for (int i = 0; i < 256; ++i)
            prevStoredEnergy += prevOverlap[i] * prevOverlap[i];

        float prevOriginalEnergy = prevStoredEnergy * prevOverlapScale;
        float prevModulatedEnergy = prevStoredEnergy / (prevDiv * prevDiv);

        Span<float> sampleDiv = stackalloc float[256];
        BuildSampleDivisors(gainPoints, sampleDiv);

        float curOriginalEnergy = 0.0f;
        float curModulatedEnergy = 0.0f;
        float nextOriginalEnergy = 0.0f;
        float nextModulatedEnergy = 0.0f;

        for (int i = 0; i < 256; ++i)
        {
            float cur = curInput[i];
            float mod = cur / sampleDiv[i];
            float winCur = CodecInfo.EncodeWindow[255 - i];
            float winNext = CodecInfo.EncodeWindow[i];
            float curWin = cur * winCur;
            float modCurWin = mod * winCur;
            float nextWin = cur * winNext;
            float modNextWin = mod * winNext;

            curOriginalEnergy += curWin * curWin;
            curModulatedEnergy += modCurWin * modCurWin;
            nextOriginalEnergy += nextWin * nextWin;
            nextModulatedEnergy += modNextWin * modNextWin;
        }

        return new GainEnergyAnalysis
        {
            Scale = new GainEnergyScale
            {
                PrevHalf = SafeEnergyScale(prevOriginalEnergy, prevModulatedEnergy),
                CurHalf = SafeEnergyScale(curOriginalEnergy, curModulatedEnergy),
                Frame = SafeEnergyScale(prevOriginalEnergy + curOriginalEnergy, prevModulatedEnergy + curModulatedEnergy)
            },
            NextOverlapScale = SafeEnergyScale(nextOriginalEnergy, nextModulatedEnergy)
        };
    }

    public ProcessResult ProcessFrame(ReadOnlySpan<float> data)
    {
        if (data.Length < CodecInfo.NumSamples * _channels)
            throw new ArgumentException("Not enough samples for a frame", nameof(data));

        int qmfOffset = _lookAheadPending ? 128 : 384;
        Span<float> src = stackalloc float[CodecInfo.NumSamples];

        for (int channel = 0; channel < _channels; channel++)
        {
            for (int i = 0; i < CodecInfo.NumSamples; ++i)
                src[i] = (float)(data[i * _channels + channel] / 4.0);

            var la = _lookAheadBuf[channel];

            _analysisFilterBank[channel].Analysis(src, la[0].AsSpan(qmfOffset, 256), la[1].AsSpan(qmfOffset, 256), la[2].AsSpan(qmfOffset, 256), la[3].AsSpan(qmfOffset, 256));
        }

        if (_lookAheadPending)
        {
            _lookAheadPending = false;
            return ProcessResult.LookAhead;
        }

        for (int channel = 0; channel < _channels; channel++)
        {
            for (int b = 0; b < 4; b++)
                _lookAheadBuf[channel][b].AsSpan(128, 256).CopyTo(_pcmBuffer[channel + b * 2].AsSpan(256, 256));
        }

        bool jsStereo = _containerParams.JointStereo && _channels == 2;
        float[][][]? jsGainInput = null;

        if (jsStereo)
        {
            if (_gainControl != null)
            {
                jsGainInput = new float[2][][];

                for (int c = 0; c < 2; c++)
                {
                    jsGainInput[c] = new float[4][];

                    for (int band = 0; band < 4; band++)
                        jsGainInput[c][band] = new float[SpectralUpsampler.InN];
                }

                for (int band = 0; band < 4; ++band)
                {
                    for (int i = 0; i < SpectralUpsampler.InN; ++i)
                    {
                        float left = _lookAheadBuf[0][band][i];
                        float right = _lookAheadBuf[1][band][i];

                        jsGainInput[0][band][i] = (left + right) * 0.5f;
                        jsGainInput[1][band][i] = (left - right) * 0.5f;
                    }
                }
            }

            Matrixing();
        }

        if (_gainControl != null)
        {
            var upAll = new float[_channels][][];

            for (int channel = 0; channel < _channels; channel++)
            {
                var up = new float[4][];

                for (int b = 0; b < 4; b++)
                    up[b] = jsStereo ? jsGainInput![channel][b] : _lookAheadBuf[channel][b];

                upAll[channel] = up;
            }

            _gainControl.Analyze(upAll, _channels);
        }

        var specs = new float[CodecInfo.NumSamples];

        for (int channel = 0; channel < _channels; channel++)
        {
            var sce = _elements[channel];

            sce.TonalBlocks.Clear();
            sce.SubbandInfo.Reset();

            for (int i = 0; i < sce.GainEnergyScale.Length; i++)
                sce.GainEnergyScale[i] = GainEnergyScale.Identity;

            _gainControl?.CreateSubbandInfo(channel, sce.SubbandInfo);

            for (int band = 0; band < CodecInfo.NumQmf; ++band)
            {
                int qmfIdx = channel + band * 2;
                var analysis = CalcGainEnergyScale(_pcmBuffer[qmfIdx].AsSpan(0, 256), _pcmBuffer[qmfIdx].AsSpan(256, 256), sce.SubbandInfo.GetGainPoints(band), _prevOverlapGainScale[channel][band]);

                sce.GainEnergyScale[band] = analysis.Scale;
                _prevOverlapGainScale[channel][band] = analysis.NextOverlapScale;
            }

            MdctBands(specs, channel, _prevPeak[channel], sce.SubbandInfo);

            float l = 0;

            for (int i = 0; i < specs.Length; i++)
            {
                float e = specs[i] * specs[i];
                int band = i / 256;

                l += e * sce.GainEnergyScale[band].Frame * _loudnessCurve[i];
            }

            sce.Loudness = l;

            if (!_noTonalComponents)
            {
                var energy = new float[specs.Length];

                for (int i = 0; i < specs.Length; i++)
                    energy[i] = specs[i] * specs[i];

                var flatnessPerBfu = PsyCommon.CalcSpectralFlatnessPerBfu(energy, CodecInfo.BlockSizeTab, CodecInfo.SpecsPerBlock, CodecInfo.MaxBfus);
                var tonals = ExtractTonalComponents(specs, flatnessPerBfu);

                sce.TonalBlocks.Clear();
                MapTonalComponents(tonals, sce.TonalBlocks);
            }

            sce.ScaledBlocks = Scaler.ScaleFrame(specs);
        }

        if (_channels == 2 && !_containerParams.JointStereo)
            _loudness = PsyCommon.TrackLoudness(_loudness, _elements[0].Loudness, _elements[1].Loudness);
        else
            _loudness = PsyCommon.TrackLoudness(_loudness, _elements[0].Loudness);

        if (_containerParams.JointStereo && _channels == 1 && _elements.Count == 1)
        {
            var empty = new SingleChannelElement();

            empty.SubbandInfo.ResizeBands(1);
            _elements.Add(empty);
        }

        _bitStreamWriter.WriteSoundUnit(_elements, _loudness / LoudFactor);

        for (int channel = 0; channel < _channels; channel++)
        {
            for (int b = 0; b < 4; b++)
                Array.Copy(_lookAheadBuf[channel][b], 256, _lookAheadBuf[channel][b], 0, 384);
        }

        return ProcessResult.Processed;
    }
}