using PBP.AT3.Enums;
using PBP.AT3.Models;
using PBP.AT3.Services;

namespace PBP.AT3.IO;

public sealed class At3BitStreamWriter
{
    private static readonly uint[] FixedBitAllocTable =
    [
        6, 6, 5, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        2, 2, 2, 2, 2, 1, 1, 1,
        1, 1, 1, 0,
        0, 0
    ];

    private const int LosyNaqStart = 18;
    private const int BoostNaqEnd = 10;

    private static float[]? s_ath;
    private static readonly object s_athLock = new();

    private readonly Writer _container;
    private readonly ContainerParams _params;
    private readonly uint _bfuIdxConst;
    private readonly BitStreamEncoder _encoder;
    private readonly List<byte> _outBuffer = [];

    public At3BitStreamWriter(Writer container, ContainerParams parameters, uint bfuIdxConst)
    {
        _container = container;
        _params = parameters;
        _bfuIdxConst = bfuIdxConst;

        EnsureAth();

        _encoder = new BitStreamEncoder( [ new ConfigurePart(), new AllocPart() ]);
    }

    private static float[] Ath
    {
        get
        {
            EnsureAth();
            return s_ath!;
        }
    }

    private static void EnsureAth()
    {
        lock (s_athLock)
        {
            if (s_ath != null)
                return;

            var ath = new List<float>(CodecInfo.MaxBfus);
            var athSpec = PsyCommon.CalcAth(1024, 44100);

            for (int bandNum = 0; bandNum < CodecInfo.NumQmf; ++bandNum)
            {
                for (int blockNum = CodecInfo.BlocksPerBand[bandNum]; blockNum < CodecInfo.BlocksPerBand[bandNum + 1]; ++blockNum)
                {
                    int specNumStart = CodecInfo.BlockSizeTab[blockNum];
                    float x = 999;

                    for (int line = specNumStart; line < specNumStart + CodecInfo.SpecsPerBlock[blockNum]; line++)
                        x = MathF.Min(x, athSpec[line]);

                    x = (float)Math.Pow(10, 0.1f * x);

                    ath.Add(x);
                }
            }

            s_ath = [.. ath];
        }
    }

    private static uint ClcEnc(uint selector, ReadOnlySpan<int> mantissas, int blockSize, BitStreamWriter? bs)
    {
        int numBits = CodecInfo.ClcLengthTab[selector];
        uint bitsUsed = (selector > 1) ? (uint)(numBits * blockSize) : (uint)(numBits * blockSize / 2);

        if (bs == null)
            return bitsUsed;

        if (selector > 1)
        {
            for (int i = 0; i < blockSize; ++i)
                bs.Write((uint)BitStreamWriter.MakeSign(mantissas[i], numBits), numBits);
        }
        else
        {
            for (int i = 0; i < blockSize / 2; ++i)
            {
                uint code = CodecInfo.MantissaToClcIdx(mantissas[i * 2]) << 2;

                code |= CodecInfo.MantissaToClcIdx(mantissas[i * 2 + 1]);

                bs.Write(code, numBits);
            }
        }

        return bitsUsed;
    }

    private static uint VlcEnc(uint selector, ReadOnlySpan<int> mantissas, int blockSize, BitStreamWriter? bs)
    {
        var table = CodecInfo.HuffTables[selector - 1];
        uint bitsUsed = 0;

        if (selector > 1)
        {
            for (int i = 0; i < blockSize; ++i)
            {
                int m = mantissas[i];
                uint huffS = (m < 0) ? (((uint)(-m)) << 1) | 1 : ((uint)m) << 1;

                if (huffS != 0)
                    huffS -= 1;

                if (huffS >= table.Length)
                    throw new InvalidOperationException("Mantissa is out of the Huffman table range");

                bitsUsed += table[huffS].Bits;

                bs?.Write(table[huffS].Code, table[huffS].Bits);
            }
        }
        else
        {
            for (int i = 0; i < blockSize / 2; ++i)
            {
                uint huffS = CodecInfo.MantissasToVlcIndex(mantissas[i * 2], mantissas[i * 2 + 1]);

                bitsUsed += table[huffS].Bits;

                bs?.Write(table[huffS].Code, table[huffS].Bits);
            }
        }

        return bitsUsed;
    }

    private sealed class SpecUnit
    {
        public int[] Mantisas = [];

        public float EnergyErr;

        public uint ClcBits;

        public uint VlcBits;
    }

    private sealed class SpecCache
    {
        private readonly SpecUnit?[] _units = new SpecUnit?[1 << 8];

        public SpecUnit GetOrCompute(int bfu, uint wordlen, float[] values)
        {
            int key = (bfu << 3) | (int)wordlen;
            var slot = _units[key];

            if (slot == null)
            {
                slot = Provide(bfu, wordlen, values);
                _units[key] = slot;
            }

            return slot;
        }

        public void Reset() => Array.Clear(_units);

        private static SpecUnit Provide(int bfu, uint wordlen, float[] values)
        {
            var u = new SpecUnit();
            int first = CodecInfo.BlockSizeTab[bfu];
            int last = CodecInfo.BlockSizeTab[bfu + 1];
            int blockSize = last - first;
            float mul = CodecInfo.MaxQuant[Math.Min(wordlen, 7u)];

            u.Mantisas = new int[blockSize];
            u.EnergyErr = MantissaQuantizer.QuantMantisas(values, 0, blockSize, mul, bfu > LosyNaqStart, u.Mantisas);
            u.ClcBits = ClcEnc(wordlen, u.Mantisas, blockSize, null);
            u.VlcBits = VlcEnc(wordlen, u.Mantisas, blockSize, null);

            return u;
        }
    }

    private static (byte Mode, uint Bits) CalcSpecsBitsConsumption(SingleChannelElement sce, uint[] precision, int[] mantisas, float[] energyErr, SpecCache cache)
    {
        var scaledBlocks = sce.ScaledBlocks;
        uint numBlocks = (uint)precision.Length;
        uint bitsUsed = numBlocks * 3;
        uint clcSpecBits = 0;
        uint vlcSpecBits = 0;

        for (int i = 0; i < numBlocks; ++i)
        {
            if (precision[i] == 0)
                continue;

            bitsUsed += 6;

            int first = CodecInfo.BlockSizeTab[i];
            int last = CodecInfo.BlockSizeTab[i + 1];
            int blockSize = last - first;
            var unit = cache.GetOrCompute(i, precision[i], scaledBlocks[i].Values);

            Array.Copy(unit.Mantisas, 0, mantisas, first, blockSize);

            energyErr[i] = unit.EnergyErr;
            clcSpecBits += unit.ClcBits;
            vlcSpecBits += unit.VlcBits;
        }

        bool mode = clcSpecBits <= vlcSpecBits;

        return ((byte)(mode ? 1 : 0), bitsUsed + (mode ? clcSpecBits : vlcSpecBits));
    }

    private static bool CheckBfus(ref ushort numBfu, uint[] precision)
    {
        ushort curLastBfu = (ushort)(numBfu - 1);

        if (precision[curLastBfu] == 0)
        {
            numBfu = curLastBfu;

            return true;
        }

        return false;
    }

    private static bool ConsiderEnergyErr(float[] err, uint[] bits)
    {
        if (err.Length < bits.Length)
            throw new InvalidOperationException();

        bool adjusted = false;
        int lim = Math.Min(BoostNaqEnd, bits.Length);

        for (int i = 0; i < lim; i++)
        {
            float e = err[i];

            if (((e > 0 && e < 0.7f) || e > 1.2f) & (bits[i] < 7))
            {
                bits[i]++;
                adjusted = true;
            }
        }

        return adjusted;
    }

    private static float SanitizeGainEnergyScale(float scale) => float.IsFinite(scale) && scale > 0.0f ? scale : 1.0f;

    private static float EnergyScaleToScaleFactorOffset(float energyScale) => 1.5f * MathF.Log2(SanitizeGainEnergyScale(energyScale));

    private static uint[] CalcBitsAllocation(SingleChannelElement sce, uint bfuNum, float spread, float shift, float loudness)
    {
        var scaledBlocks = sce.ScaledBlocks;
        var ath = Ath;
        var bitsPerEachBlock = new uint[bfuNum];

        for (int i = 0; i < bitsPerEachBlock.Length; ++i)
        {
            int bfuBand = 0;

            for (int b = 1; b < CodecInfo.NumQmf; ++b)
            {
                if (i >= CodecInfo.BlocksPerBand[b])
                    bfuBand = b;
            }

            float gainEnergyScale = SanitizeGainEnergyScale(sce.GainEnergyScale[bfuBand].Frame);
            float correctedEnergy = scaledBlocks[i].Energy * gainEnergyScale;
            float athLimit = ath[i] * loudness;

            if (correctedEnergy < athLimit)
                bitsPerEachBlock[i] = 0;
            else
            {
                uint fix = FixedBitAllocTable[i];
                float x = 6;

                if (i < 3)
                    x = (float)2.8;
                else if (i < 10)
                    x = (float)2.6;
                else if (i < 15)
                    x = (float)3.3;
                else if (i <= 20)
                    x = (float)3.6;
                else if (i <= 28)
                    x = (float)4.2;

                float correctedScaleFactorIndex = MathF.Max(0.0f, MathF.Min(63.0f, (float)scaledBlocks[i].ScaleFactorIndex + EnergyScaleToScaleFactorOffset(gainEnergyScale)));
                int tmp = (int)(spread * (correctedScaleFactorIndex / x) + (1.0f - spread) * fix - shift);

                if (tmp > 7)
                    bitsPerEachBlock[i] = 7;
                else if (tmp < 0)
                    bitsPerEachBlock[i] = 0;
                else if (tmp == 0)
                    bitsPerEachBlock[i] = 1;
                else
                    bitsPerEachBlock[i] = (uint)tmp;
            }
        }

        foreach (var tc in sce.TonalBlocks)
        {
            if (tc.Val.Bfu < bitsPerEachBlock.Length)
            {
                if (bitsPerEachBlock[tc.Val.Bfu] > 2)
                    bitsPerEachBlock[tc.Val.Bfu] -= 1;
            }
        }

        return bitsPerEachBlock;
    }

    private sealed class TonalSubGroup
    {
        public readonly List<byte> SubGroupMap = [];

        public readonly List<TonalBlock> SubGroupPtr = [];
    }

    private static uint GroupTonalComponents(List<TonalBlock> tonalComponents, uint[] allocTable, TonalSubGroup[] groups)
    {
        foreach (var tc in tonalComponents)
        {
            uint bfu = tc.Val.Bfu;

            if (bfu >= allocTable.Length)
                continue;

            uint quant = Math.Max(2u, Math.Min(allocTable[bfu] + 4, 7u));

            groups[quant * 8 + tc.ScaledBlock.Values.Length].SubGroupPtr.Add(tc);
        }

        uint tcsgn = 0;

        for (int i = 0; i < 64; ++i)
        {
            int startPos;
            int curPos = 0;

            while (curPos < groups[i].SubGroupPtr.Count)
            {
                startPos = curPos;
                ++tcsgn;

                groups[i].SubGroupMap.Add((byte)curPos);

                uint groupLimiter = 0;

                do
                {
                    ++curPos;

                    if (curPos == groups[i].SubGroupPtr.Count)
                        break;

                    if (groups[i].SubGroupPtr[curPos].Val.Pos - (groups[i].SubGroupPtr[startPos].Val.Pos & ~63) < 64)
                        ++groupLimiter;
                    else
                    {
                        groupLimiter = 0;
                        startPos = curPos;
                    }
                } while (groupLimiter < 7u);
            }
        }
        return tcsgn;
    }

    private static uint EncodeTonalComponents(SingleChannelElement sce, uint[] allocTable, BitStreamWriter? bs)
    {
        var tonalComponents = sce.TonalBlocks;
        int numQmfBand = sce.SubbandInfo.GetQmfNum();
        uint bitsUsed = 0;
        var groups = new TonalSubGroup[64];

        for (int i = 0; i < 64; i++)
            groups[i] = new TonalSubGroup();

        uint tcsgn = GroupTonalComponents(tonalComponents, allocTable, groups);

        if (tcsgn >= 32)
            throw new InvalidOperationException("Too many tonal component groups");

        bitsUsed += 5;

        bs?.Write(tcsgn, 5);

        if (tcsgn == 0)
            return bitsUsed;

        bitsUsed += 2;

        bs?.Write(0, 2);

        Span<int> mantisas = stackalloc int[256];

        for (int i = 0; i < 64; ++i)
        {
            var curGroup = groups[i];

            if (curGroup.SubGroupPtr.Count == 0)
                continue;

            for (int subgroup = 0; subgroup < curGroup.SubGroupMap.Count; ++subgroup)
            {
                int subGroupStartPos = curGroup.SubGroupMap[subgroup];
                int subGroupEndPos = (subgroup < curGroup.SubGroupMap.Count - 1) ? curGroup.SubGroupMap[subgroup + 1] : curGroup.SubGroupPtr.Count;
                int codedValues = curGroup.SubGroupPtr[0].ScaledBlock.Values.Length;
                var bandCount = new byte[16];

                for (int j = subGroupStartPos; j < subGroupEndPos; ++j)
                {
                    int specBlock = curGroup.SubGroupPtr[j].Val.Pos >> 6;

                    bandCount[specBlock]++;
                }

                bool BandHasComponents(int band) => bandCount[band * 4] != 0 || bandCount[band * 4 + 1] != 0 || bandCount[band * 4 + 2] != 0 || bandCount[band * 4 + 3] != 0;

                bitsUsed += (uint)numQmfBand;

                if (bs != null)
                {
                    for (int j = 0; j < numQmfBand; ++j)
                        bs.Write(BandHasComponents(j) ? 1u : 0u, 1);
                }

                bitsUsed += 3;

                bs?.Write((uint)(codedValues - 1), 3);

                bitsUsed += 3;

                bs?.Write((uint)(i >> 3), 3);

                int lastPos = subGroupStartPos;

                for (int j = 0; j < 16; ++j)
                {
                    if (!BandHasComponents(j >> 2))
                        continue;

                    int codedComponents = bandCount[j];

                    bitsUsed += 3;

                    bs?.Write((uint)codedComponents, 3);

                    int k = lastPos;

                    for (; k < lastPos + codedComponents; ++k)
                    {
                        var block = curGroup.SubGroupPtr[k];
                        int relPos = block.Val.Pos - j * 64;

                        bitsUsed += 6;

                        bs?.Write(block.ScaledBlock.ScaleFactorIndex, 6);

                        bitsUsed += 6;

                        bs?.Write((uint)relPos, 6);

                        float mul = CodecInfo.MaxQuant[Math.Min((uint)(i >> 3), 7u)];

                        for (int z = 0; z < block.ScaledBlock.Values.Length; ++z)
                            mantisas[z] = MantissaQuantizer.ToInt(block.ScaledBlock.Values[z] * mul);

                        bitsUsed += VlcEnc((uint)(i >> 3), mantisas, block.ScaledBlock.Values.Length, bs);
                    }

                    lastPos = k;
                }
            }
        }

        return bitsUsed;
    }

    private static void EncodeSpecs(SingleChannelElement sce, BitStreamWriter bs, uint[] precision, byte codingMode, int[] mt)
    {
        var scaledBlocks = sce.ScaledBlocks;

        EncodeTonalComponents(sce, precision, bs);

        int numBlocks = precision.Length;

        bs.Write((uint)(numBlocks - 1), 5);
        bs.Write(codingMode, 1);

        for (int i = 0; i < numBlocks; ++i)
            bs.Write(precision[i], 3);

        for (int i = 0; i < numBlocks; ++i)
        {
            if (precision[i] == 0)
                continue;

            bs.Write(scaledBlocks[i].ScaleFactorIndex, 6);
        }

        for (int i = 0; i < numBlocks; ++i)
        {
            if (precision[i] == 0)
                continue;

            int first = CodecInfo.BlockSizeTab[i];
            int last = CodecInfo.BlockSizeTab[i + 1];
            int blockSize = last - first;

            if (codingMode == 1)
                ClcEnc(precision[i], mt.AsSpan(first, blockSize), blockSize, bs);
            else
                VlcEnc(precision[i], mt.AsSpan(first, blockSize), blockSize, bs);
        }
    }

    private static ushort CalcInitialNumBfu(uint bfuIdxConst, ushort targetBits)
    {
        ushort numBfu = (ushort)(bfuIdxConst != 0 ? bfuIdxConst : 32);

        if (targetBits < 101)
        {
            ushort lim = 1;

            if (targetBits > 5)
                lim = (ushort)((targetBits - 5) / 3);

            lim = Math.Max((ushort)1, lim);
            numBfu = Math.Min(numBfu, lim);
        }

        return Math.Max((ushort)1, numBfu);
    }

    private sealed class ConfigurePart : IBitStreamPartEncoder
    {
        public PartStatus Encode(EncodeCtx ctx, BitAllocHandler ba)
        {
            if (ctx.Sce.ScaledBlocks.Count == 0)
            {
                ctx.AllocInitDone = true;
                ctx.NumBfu = 1;
                ctx.Spread = 0.0f;
                ctx.CodingMode = 1;
                ctx.PrecisionPerBlock = new uint[1];

                return PartStatus.Ok;
            }

            if (!ctx.AllocInitDone)
            {
                ctx.Spread = PsyCommon.AnalizeScaleFactorSpread(ctx.Sce.ScaledBlocks);
                ctx.NumBfu = CalcInitialNumBfu(ctx.BfuIdxConst, ctx.TargetBits);
                ctx.AllocInitDone = true;
            }

            ctx.PrecisionPerBlock = new uint[ctx.NumBfu];
            ctx.EnergyErr = new float[ctx.NumBfu];

            ba.Start(ctx.TargetBits, -8.0f, 20.0f);

            return PartStatus.Ok;
        }

        public void Dump(BitStreamWriter bs) { }

        public void Reset() { }
    }

    private sealed class AllocPart : IBitStreamPartEncoder
    {
        private EncodeCtx? _ctx;
        private readonly SpecCache _specCache = new();

        public PartStatus Encode(EncodeCtx ctx, BitAllocHandler ba)
        {
            if (ctx.Sce.ScaledBlocks.Count == 0)
            {
                _ctx = ctx;
                return PartStatus.Ok;
            }

            float shift = ba.Continue();
            uint[] tmpAlloc = CalcBitsAllocation(ctx.Sce, (uint)ctx.NumBfu, ctx.Spread, shift, ctx.Loudness);

            ctx.EnergyErr = new float[ctx.NumBfu];

            (byte Mode, uint Bits) consumption;

            do
            {
                consumption = CalcSpecsBitsConsumption(ctx.Sce, tmpAlloc, ctx.Mantissas, ctx.EnergyErr, _specCache);
            } while (ConsiderEnergyErr(ctx.EnergyErr, tmpAlloc));

            uint totalBits = consumption.Bits + EncodeTonalComponents(ctx.Sce, tmpAlloc, null);

            if (ba.Submit(totalBits))
            {
                if (ctx.BfuIdxConst == 0 && ctx.NumBfu > 1)
                {
                    ushort numBfu = (ushort)ctx.NumBfu;

                    if (CheckBfus(ref numBfu, tmpAlloc))
                    {
                        ctx.NumBfu = numBfu;
                        return PartStatus.Repeat;
                    }
                }

                ctx.PrecisionPerBlock = tmpAlloc;
                ctx.CodingMode = consumption.Mode;
                _ctx = ctx;
            }

            return PartStatus.Ok;
        }

        public void Dump(BitStreamWriter bs)
        {
            if (_ctx != null)
            {
                EncodeSpecs(_ctx.Sce, bs, _ctx.PrecisionPerBlock, _ctx.CodingMode, _ctx.Mantissas);
                _ctx = null;
            }

            _specCache.Reset();
        }

        public void Reset() => _ctx = null;
    }

    private static void WriteJsParams(BitStreamWriter bs)
    {
        bs.Write(0, 1);
        bs.Write(7, 3);

        for (int i = 0; i < 4; i++)
            bs.Write(3, 2);
    }

    private static float CalcMsRatio(float mEnergy, float sEnergy)
    {
        float total = sEnergy + mEnergy;

        if (total > 0)
            return (float)(mEnergy / total - 0.5);

        return 0;
    }

    private static int CalcMsBytesShift(uint frameSz, List<SingleChannelElement> elements, int[] b)
    {
        int totalUsedBits = 0 - b[0] - b[1];

        if (totalUsedBits <= 0)
            throw new InvalidOperationException();

        int maxAllowedShift = unchecked((int)(frameSz / 2 - Div8Ceil((uint)totalUsedBits)));

        if (elements[1].ScaledBlocks.Count == 0)
            return maxAllowedShift;

        float ratio = CalcMsRatio(elements[0].Loudness, elements[1].Loudness);

        return Math.Max(Math.Min(MantissaQuantizer.ToInt(frameSz * ratio), maxAllowedShift), -maxAllowedShift);
    }

    private static uint Div8Ceil(uint value) => unchecked(1 + (value - 1) / 8);

    public void WriteSoundUnit(List<SingleChannelElement> elements, float loudness)
    {
        if (elements.Count != 1 && elements.Count != 2)
            throw new ArgumentException("Wrong number of elements", nameof(elements));

        int halfFrameSz = _params.FrameSize >> 1;
        var bitStreams = new BitStreamWriter[2];
        var bitsToAlloc = new[] { -6, -6 };

        for (int channel = 0; channel < elements.Count; channel++)
        {
            var sce = elements[channel];
            var subbandInfo = sce.SubbandInfo;
            var bs = new BitStreamWriter();

            bitStreams[channel] = bs;

            if (_params.JointStereo && channel == 1)
            {
                WriteJsParams(bs);
                bs.Write(3, 2);
            }
            else
                bs.Write(0x28, 6);

            int numQmfBand = subbandInfo.GetQmfNum();

            bs.Write((uint)(numQmfBand - 1), 2);

            for (int band = 0; band < numQmfBand; ++band)
            {
                var gainPoints = subbandInfo.GetGainPoints(band);

                bs.Write((uint)gainPoints.Count, 3);

                foreach (var point in gainPoints)
                {
                    bs.Write(point.Level, 4);
                    bs.Write(point.Location, 5);
                }
            }

            short bitsUsedByGainInfoAndHeader = (short)bs.SizeInBits;

            bitsToAlloc[channel] -= bitsUsedByGainInfoAndHeader;
        }

        int msBytesShift = _params.JointStereo ? CalcMsBytesShift(_params.FrameSize, elements, bitsToAlloc) : 0;

        bitsToAlloc[0] += 8 * (halfFrameSz + msBytesShift);
        bitsToAlloc[1] += 8 * (halfFrameSz - msBytesShift);

        for (int channel = 0; channel < elements.Count; channel++)
        {
            var sce = elements[channel];
            var bs = bitStreams[channel];
            var ctx = new EncodeCtx
            {
                Sce = sce,
                TargetBits = (ushort)Math.Max(1, bitsToAlloc[channel]),
                BfuIdxConst = _bfuIdxConst,
                Loudness = loudness
            };

            _encoder.Do(ctx, bs);

            var bytes = bs.Bytes;

            if (_params.JointStereo && channel == 1)
            {
                var channelData = new byte[halfFrameSz - msBytesShift];

                bytes[..Math.Min(bytes.Length, channelData.Length)].CopyTo(channelData);

                for (int i = channelData.Length - 1; i >= 0; i--)
                    _outBuffer.Add(channelData[i]);
            }
            else
            {
                var channelData = new byte[halfFrameSz + msBytesShift];

                bytes[..Math.Min(bytes.Length, channelData.Length)].CopyTo(channelData);
                _outBuffer.AddRange(channelData);
            }
        }

        if (elements.Count == 1 && !_params.JointStereo)
        {
            int sz = _outBuffer.Count;

            if (sz != halfFrameSz)
                throw new InvalidOperationException();

            _outBuffer.AddRange(_outBuffer.GetRange(0, sz));
        }

        _container.WriteFrame(_outBuffer.ToArray());
        _outBuffer.Clear();
    }
}