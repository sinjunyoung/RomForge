using PSP.PBP.AT3.IO;
using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

public sealed class Decoder
{
    private const int Bands = 4;
    private const int BandSamples = 256;

    private readonly int _channels;
    private readonly int _frameSize;
    private readonly bool _jointStereo;
    private readonly ChannelState[] _states;
    private readonly ChannelUnit[] _units;
    private readonly float[][][] _sub;
    private readonly float[][] _pcm;
    private readonly Midct _midct = new();
    private readonly float[] _imdct = new float[Midct.OutputSize];
    private readonly float[] _band = new float[BandSamples];
    private readonly int[] _mantissas = new int[CodecInfo.MaxSpecs];
    private readonly byte[] _reversed;

    public Decoder(int channels, int frameSize, bool jointStereo)
    {
        if (channels != 1 && channels != 2)
            throw new ArgumentException("모노 또는 스테레오만 지원합니다.", nameof(channels));

        if (frameSize < 8)
            throw new ArgumentException("프레임 크기가 올바르지 않습니다.", nameof(frameSize));

        _channels = channels;
        _frameSize = frameSize;
        _jointStereo = jointStereo && channels == 2;
        _states = new ChannelState[channels];
        _units = new ChannelUnit[channels];
        _sub = new float[channels][][];
        _pcm = new float[channels][];
        _reversed = new byte[frameSize];

        for (int c = 0; c < channels; c++)
        {
            _states[c] = new ChannelState();
            _units[c] = new ChannelUnit();
            _sub[c] = new float[Bands][];
            _pcm[c] = new float[CodecInfo.NumSamples];

            for (int b = 0; b < Bands; b++)
                _sub[c][b] = new float[BandSamples];
        }
    }

    public int Channels => _channels;

    public void DecodeFrame(byte[] data, int offset, Span<float> output)
    {
        if (output.Length < CodecInfo.NumSamples * _channels)
            throw new ArgumentException("출력 버퍼가 작습니다.", nameof(output));

        if (_channels == 1)
            ParseUnit(new BitStreamReader(data, offset, _frameSize), false, _units[0]);
        else if (_jointStereo)
        {
            ParseUnit(new BitStreamReader(data, offset, _frameSize), false, _units[0]);

            for (int i = 0; i < _frameSize; i++)
                _reversed[i] = data[offset + _frameSize - 1 - i];

            int skip = 0;

            while (skip < _frameSize && _reversed[skip] == 0xF8)
                skip++;

            ParseUnit(new BitStreamReader(_reversed, skip, _frameSize - skip), true, _units[1]);
        }
        else
        {
            int half = _frameSize / 2;

            ParseUnit(new BitStreamReader(data, offset, half), false, _units[0]);
            ParseUnit(new BitStreamReader(data, offset + half, half), false, _units[1]);
        }

        for (int c = 0; c < _channels; c++)
            Reconstruct(c);

        if (_jointStereo)
        {
            for (int b = 0; b < Bands; b++)
            {
                var m = _sub[0][b];
                var s = _sub[1][b];

                for (int i = 0; i < BandSamples; i++)
                {
                    float mid = m[i];
                    float side = s[i];

                    m[i] = mid + side;
                    s[i] = mid - side;
                }
            }
        }

        for (int c = 0; c < _channels; c++)
            _states[c].Synthesis.Synthesis(_sub[c][0], _sub[c][1], _sub[c][2], _sub[c][3], _pcm[c]);

        for (int i = 0; i < CodecInfo.NumSamples; i++)
        {
            for (int c = 0; c < _channels; c++)
                output[i * _channels + c] = _pcm[c][i];
        }
    }

    private void ParseUnit(BitStreamReader br, bool secondJointChannel, ChannelUnit unit)
    {
        if (secondJointChannel)
        {
            br.Read(1);
            br.Read(3);

            for (int i = 0; i < 4; i++)
                br.Read(2);

            if (br.Read(2) != 3)
                throw new InvalidDataException("조인트 스테레오 유닛 헤더가 올바르지 않습니다.");
        }
        else if (br.Read(6) != 0x28)
            throw new InvalidDataException("사운드 유닛 헤더가 올바르지 않습니다.");

        unit.NumBands = (int)br.Read(2) + 1;

        for (int b = 0; b < Bands; b++)
        {
            unit.Gain[b].Clear();

            if (b >= unit.NumBands)
                continue;

            int count = (int)br.Read(3);

            for (int j = 0; j < count; j++)
            {
                uint level = br.Read(4);
                uint location = br.Read(5);

                unit.Gain[b].Add(new GainPoint(level, location));
            }
        }

        unit.Tonals.Clear();
        ParseTonalComponents(br, unit);
        Array.Clear(unit.Spectrum);
        ParseSpectrum(br, unit);

        foreach (var (pos, coefs) in unit.Tonals)
        {
            for (int j = 0; j < coefs.Length; j++)
                unit.Spectrum[pos + j] += coefs[j];
        }

        if (br.Overrun)
            throw new InvalidDataException("비트스트림이 예상보다 짧습니다.");
    }

    private void ParseTonalComponents(BitStreamReader br, ChannelUnit unit)
    {
        int groups = (int)br.Read(5);

        if (groups == 0)
            return;

        int modeSelector = (int)br.Read(2);

        if (modeSelector == 2)
            throw new InvalidDataException("톤 성분 코딩 모드가 올바르지 않습니다.");

        bool constantLength = (modeSelector & 1) != 0;
        Span<bool> bandFlags = stackalloc bool[Bands];

        for (int g = 0; g < groups; g++)
        {
            for (int b = 0; b < unit.NumBands; b++)
                bandFlags[b] = br.ReadBit() != 0;

            int codedValues = (int)br.Read(3) + 1;
            int quant = (int)br.Read(3);

            if (quant <= 1)
                throw new InvalidDataException("톤 성분 양자화 인덱스가 올바르지 않습니다.");

            if (modeSelector == 3)
                constantLength = br.ReadBit() != 0;

            for (int b = 0; b < unit.NumBands * 4; b++)
            {
                if (!bandFlags[b >> 2])
                    continue;

                int components = (int)br.Read(3);

                for (int c = 0; c < components; c++)
                {
                    int scaleIndex = (int)br.Read(6);
                    int pos = b * 64 + (int)br.Read(6);
                    int count = Math.Min(codedValues, CodecInfo.NumSpecs - pos);

                    if (count <= 0)
                        throw new InvalidDataException("톤 성분 위치가 올바르지 않습니다.");

                    MantissaReader.Read(br, quant, constantLength, count, _mantissas);

                    float factor = CodecInfo.ScaleTable[scaleIndex] / CodecInfo.MaxQuant[quant];
                    var coefs = new float[count];

                    for (int m = 0; m < count; m++)
                        coefs[m] = _mantissas[m] * factor;

                    unit.Tonals.Add((pos, coefs));
                }
            }
        }
    }

    private void ParseSpectrum(BitStreamReader br, ChannelUnit unit)
    {
        int numBlocks = (int)br.Read(5) + 1;
        bool constantLength = br.ReadBit() != 0;
        Span<int> precision = stackalloc int[CodecInfo.MaxBfus];
        Span<int> scaleIndex = stackalloc int[CodecInfo.MaxBfus];

        for (int i = 0; i < numBlocks; i++)
            precision[i] = (int)br.Read(3);

        for (int i = 0; i < numBlocks; i++)
        {
            if (precision[i] != 0)
                scaleIndex[i] = (int)br.Read(6);
        }

        for (int i = 0; i < numBlocks; i++)
        {
            if (precision[i] == 0)
                continue;

            int first = CodecInfo.BlockSizeTab[i];
            int size = CodecInfo.BlockSizeTab[i + 1] - first;

            MantissaReader.Read(br, precision[i], constantLength, size, _mantissas);

            float factor = CodecInfo.ScaleTable[scaleIndex[i]] / CodecInfo.MaxQuant[precision[i]];

            for (int j = 0; j < size; j++)
                unit.Spectrum[first + j] = _mantissas[j] * factor;
        }
    }

    private void Reconstruct(int channel)
    {
        var unit = _units[channel];
        var state = _states[channel];

        for (int band = 0; band < Bands; band++)
        {
            Array.Copy(unit.Spectrum, band * BandSamples, _band, 0, BandSamples);

            if ((band & 1) != 0)
                Array.Reverse(_band);

            _midct.Transform(_band, _imdct);

            for (int i = 0; i < BandSamples; i++)
            {
                _imdct[i] *= CodecInfo.DecodeWindow[i];
                _imdct[BandSamples + i] *= CodecInfo.DecodeWindow[BandSamples - 1 - i];
            }

            ApplyGain(_imdct, state.Overlap[band], state.Gain[band], unit.Gain[band], _sub[channel][band]);
            state.Gain[band].Clear();
            state.Gain[band].AddRange(unit.Gain[band]);
        }
    }

    private static void ApplyGain(float[] input, float[] overlap, List<GainPoint> now, List<GainPoint> next, float[] output)
    {
        float scale = next.Count > 0 ? CodecInfo.GainLevel[next[0].Level] : 1.0f;

        if (now.Count == 0)
        {
            for (int pos = 0; pos < BandSamples; pos++)
                output[pos] = input[pos] * scale + overlap[pos];
        }
        else
        {
            int pos = 0;

            for (int i = 0; i < now.Count; i++)
            {
                int lastPos = (int)(now[i].Location << CodecInfo.LocScale);
                float level = CodecInfo.GainLevel[now[i].Level];
                int nextLevel = i + 1 < now.Count ? (int)now[i + 1].Level : CodecInfo.ExponentOffset;
                float inc = CodecInfo.GainInterpolation[nextLevel - (int)now[i].Level + CodecInfo.GainInterpolationPosShift];

                for (; pos < lastPos && pos < BandSamples; pos++)
                    output[pos] = (input[pos] * scale + overlap[pos]) * level;

                for (; pos < lastPos + CodecInfo.LocSz && pos < BandSamples; pos++)
                {
                    output[pos] = (input[pos] * scale + overlap[pos]) * level;
                    level *= inc;
                }
            }

            for (; pos < BandSamples; pos++)
                output[pos] = input[pos] * scale + overlap[pos];
        }

        Array.Copy(input, BandSamples, overlap, 0, BandSamples);
    }

    private sealed class ChannelUnit
    {
        public int NumBands = Bands;

        public readonly List<GainPoint>[] Gain = [[], [], [], []];

        public readonly List<(int Pos, float[] Coefs)> Tonals = [];

        public readonly float[] Spectrum = new float[CodecInfo.NumSpecs];
    }

    private sealed class ChannelState
    {
        public readonly float[][] Overlap = [new float[BandSamples], new float[BandSamples], new float[BandSamples], new float[BandSamples]];

        public readonly List<GainPoint>[] Gain = [[], [], [], []];

        public readonly SynthesisFilterBank Synthesis = new();
    }
}