using Patch.Core.Formats.Xdelta.Models;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Patch.Core.Formats.Xdelta.Services;

internal sealed class LzmaDecoder
{
    private readonly ushort[] _isMatch;
    private readonly ushort[] _isRep;
    private readonly ushort[] _isRepG0;
    private readonly ushort[] _isRepG1;
    private readonly ushort[] _isRepG2;
    private readonly ushort[] _isRep0Long;
    private readonly ushort[] _posSlotCoders;
    private readonly ushort[] _posSpecProbs;
    private readonly ushort[] _alignProbs;
    private readonly ushort[] _litProbs;

    private readonly ushort[] _matchLenProbs;
    private readonly ushort[] _repLenProbs;

    private int _lc, _lp, _pb;
    private int _posMask;
    private int _litPosMask;

    private int _state;
    private int _rep0, _rep1, _rep2, _rep3;

    private const int kLenChoice = 0;
    private const int kLenChoice2 = 1;
    private const int kLenLow = 2;

    private const int kLenMid = kLenLow + (LzmaConstants.kNumPosStatesMax << LzmaConstants.kNumLowLenBits);
    private const int kLenHigh = kLenMid + (LzmaConstants.kNumPosStatesMax << LzmaConstants.kNumMidLenBits);

    public int LcLp => _lc + _lp;

    public LzmaDecoder(int lc, int lp, int pb)
    {
        _lc = lc;
        _lp = lp;
        _pb = pb;
        _posMask = (1 << pb) - 1;
        _litPosMask = (1 << lp) - 1;

        _isMatch = new ushort[LzmaConstants.kNumStates * LzmaConstants.kNumPosStatesMax];
        _isRep = new ushort[LzmaConstants.kNumStates];
        _isRepG0 = new ushort[LzmaConstants.kNumStates];
        _isRepG1 = new ushort[LzmaConstants.kNumStates];
        _isRepG2 = new ushort[LzmaConstants.kNumStates];
        _isRep0Long = new ushort[LzmaConstants.kNumStates * LzmaConstants.kNumPosStatesMax];

        _posSlotCoders = new ushort[LzmaConstants.kNumLenToPosStates * LzmaConstants.kNumPosSlots];
        _posSpecProbs = new ushort[LzmaConstants.kNumFullDistances - LzmaConstants.kEndPosModelIndex];
        _alignProbs = new ushort[LzmaConstants.kAlignTableSize];

        int numLitSubcoders = 1 << (lc + lp);

        _litProbs = new ushort[numLitSubcoders * LzmaConstants.kLitSubcoderSize];

        int lenProbs = 2 + (LzmaConstants.kNumPosStatesMax << LzmaConstants.kNumLowLenBits) + (LzmaConstants.kNumPosStatesMax << LzmaConstants.kNumMidLenBits) + (1 << LzmaConstants.kNumHighLenBits);

        _matchLenProbs = new ushort[lenProbs];
        _repLenProbs = new ushort[lenProbs];

        ResetState();
    }

    public void ResetState()
    {
        _state = 0;
        _rep0 = _rep1 = _rep2 = _rep3 = 0;

        RangeDecoder.InitProbs(_isMatch);
        RangeDecoder.InitProbs(_isRep);
        RangeDecoder.InitProbs(_isRepG0);
        RangeDecoder.InitProbs(_isRepG1);
        RangeDecoder.InitProbs(_isRepG2);
        RangeDecoder.InitProbs(_isRep0Long);
        RangeDecoder.InitProbs(_posSlotCoders);
        RangeDecoder.InitProbs(_posSpecProbs);
        RangeDecoder.InitProbs(_alignProbs);
        RangeDecoder.InitProbs(_litProbs);
        RangeDecoder.InitProbs(_matchLenProbs);
        RangeDecoder.InitProbs(_repLenProbs);
    }

    public void SetProperties(int lc, int lp, int pb)
    {
        _lc = lc;
        _lp = lp;
        _pb = pb;
        _posMask = (1 << pb) - 1;
        _litPosMask = (1 << lp) - 1;
    }

    public void Decode(ReadOnlyMemory<byte> input, int inputOffset, Span<byte> output, ref int outPos, long uncompressedSize)
    {
        if (uncompressedSize < 0 || uncompressedSize > output.Length - outPos)
            throw new LzmaDataErrorException("Output buffer is too small for the LZMA data.");

        var rc = new RangeDecoder();

        rc.Init(input.Span, inputOffset);
        DecodeChunk(ref rc, output, ref outPos, 0, (int)uncompressedSize);
    }

    public void DecodeChunk(ref RangeDecoder rc, Span<byte> output, ref int outPos, int dictStart, int uncompressedSize) => DecodeCore(ref rc, output, ref outPos, dictStart, uncompressedSize, exactSize: true, allowEndMarker: false);

    public bool DecodeWithEndMarker(ref RangeDecoder rc, Span<byte> output, ref int outPos, int dictStart, int softTarget) => DecodeCore(ref rc, output, ref outPos, dictStart, softTarget, exactSize: false, allowEndMarker: true);

    private bool DecodeCore(ref RangeDecoder rc, Span<byte> output, ref int outPos, int dictStart, int uncompressedSize, bool exactSize, bool allowEndMarker)
    {
        int slack = exactSize ? 0 : LzmaConstants.kMatchMaxLen;

        if ((uint)dictStart > (uint)outPos || uncompressedSize < 0 || uncompressedSize > output.Length - outPos - slack)
            throw new LzmaDataErrorException("Output buffer is too small for the LZMA data.");

        int state = _state;
        int rep0 = _rep0, rep1 = _rep1, rep2 = _rep2, rep3 = _rep3;

        if (state >= 7 && (rep0 < 0 || rep0 >= outPos - dictStart))
            throw new LzmaDataErrorException("Invalid rep distance at chunk start.");

        int posMask = _posMask;
        int litPosMask = _litPosMask;
        int lc = _lc;
        int pos = outPos;
        int remaining = uncompressedSize;
        ref ushort isMatchRoot = ref MemoryMarshal.GetArrayDataReference(_isMatch);
        ref ushort isRepRoot = ref MemoryMarshal.GetArrayDataReference(_isRep);
        ref ushort isRepG0Root = ref MemoryMarshal.GetArrayDataReference(_isRepG0);
        ref ushort isRepG1Root = ref MemoryMarshal.GetArrayDataReference(_isRepG1);
        ref ushort isRepG2Root = ref MemoryMarshal.GetArrayDataReference(_isRepG2);
        ref ushort isRep0LongRoot = ref MemoryMarshal.GetArrayDataReference(_isRep0Long);
        ref ushort litRoot = ref MemoryMarshal.GetArrayDataReference(_litProbs);

        while (remaining > 0)
        {
            int posState = (pos - dictStart) & posMask;

            if (rc.DecodeBit(ref Unsafe.Add(ref isMatchRoot, (state << LzmaConstants.kNumPosStatesBitsMax) + posState)) == 0)
            {
                byte prevByte = pos > dictStart ? output[pos - 1] : (byte)0;
                int litState = (((pos - dictStart) & litPosMask) << lc) + (prevByte >> (8 - lc));
                ref ushort litSub = ref Unsafe.Add(ref litRoot, litState * LzmaConstants.kLitSubcoderSize);
                uint symbol = 1;

                if (state >= 7)
                {
                    byte matchByte = output[pos - rep0 - 1];

                    do
                    {
                        uint matchBit = (uint)(matchByte >> 7) & 1;

                        matchByte <<= 1;

                        uint bit = rc.DecodeBit(ref Unsafe.Add(ref litSub, (int)(((1 + matchBit) << 8) + symbol)));

                        symbol = (symbol << 1) | bit;

                        if (matchBit != bit)
                            break;

                    } while (symbol < 0x100);
                }

                while (symbol < 0x100)
                    symbol = (symbol << 1) | rc.DecodeBit(ref Unsafe.Add(ref litSub, (int)symbol));

                output[pos++] = (byte)symbol;

                state = LzmaConstants.StateUpdateLiteral(state);

                remaining--;
            }
            else
            {
                int len;

                if (rc.DecodeBit(ref Unsafe.Add(ref isRepRoot, state)) != 0)
                {
                    if (rc.DecodeBit(ref Unsafe.Add(ref isRepG0Root, state)) == 0)
                    {
                        if (rc.DecodeBit(ref Unsafe.Add(ref isRep0LongRoot, (state << LzmaConstants.kNumPosStatesBitsMax) + posState)) == 0)
                        {
                            if (rep0 >= pos - dictStart)
                                throw new LzmaDataErrorException("Invalid distance in short rep.");

                            output[pos] = output[pos - rep0 - 1];
                            pos++;
                            state = LzmaConstants.StateUpdateShortRep(state);
                            remaining--;

                            continue;
                        }
                    }
                    else
                    {
                        int dist;

                        if (rc.DecodeBit(ref Unsafe.Add(ref isRepG1Root, state)) == 0)
                            dist = rep1;
                        else
                        {
                            if (rc.DecodeBit(ref Unsafe.Add(ref isRepG2Root, state)) == 0)
                                dist = rep2;
                            else
                            {
                                dist = rep3;
                                rep3 = rep2;
                            }

                            rep2 = rep1;
                        }

                        rep1 = rep0;
                        rep0 = dist;
                    }

                    len = DecodeLength(ref rc, _repLenProbs, posState);
                    state = LzmaConstants.StateUpdateLongRep(state);
                }
                else
                {
                    rep3 = rep2;
                    rep2 = rep1;
                    rep1 = rep0;
                    len = DecodeLength(ref rc, _matchLenProbs, posState);

                    int distSlot = DecodeDistSlot(ref rc, LzmaConstants.GetLenToPosState(len + LzmaConstants.kMatchMinLen));

                    rep0 = DecodeDistance(ref rc, distSlot);
                    state = LzmaConstants.StateUpdateMatch(state);
                }

                len += LzmaConstants.kMatchMinLen;

                if (allowEndMarker && rep0 == -1)
                {
                    outPos = pos;
                    _state = state;
                    _rep0 = rep0;
                    _rep1 = rep1;
                    _rep2 = rep2;
                    _rep3 = rep3;

                    return true;
                }

                if (exactSize && len > remaining)
                    throw new LzmaDataErrorException("LZMA match exceeds chunk boundary.");

                if (rep0 < 0 || rep0 >= pos - dictStart)
                    throw new LzmaDataErrorException("Invalid match distance.");

                CopyMatch(output, pos, rep0, len);

                pos += len;
                remaining -= len;
            }
        }

        outPos = pos;
        _state = state;
        _rep0 = rep0;
        _rep1 = rep1;
        _rep2 = rep2;
        _rep3 = rep3;

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMatch(Span<byte> output, int pos, int dist, int len)
    {
        int src = pos - dist - 1;

        if (dist == 0)
            output.Slice(pos, len).Fill(output[src]);
        else if (dist + 1 >= len)
            output.Slice(src, len).CopyTo(output.Slice(pos, len));
        else
        {
            int dstPos = pos;
            int remaining = len;

            while (remaining > 0)
            {
                int chunk = Math.Min(remaining, dstPos - src);

                output.Slice(src, chunk).CopyTo(output.Slice(dstPos, chunk));

                dstPos += chunk;
                remaining -= chunk;
            }
        }
    }

    private static int DecodeLength(ref RangeDecoder rc, ushort[] lenProbs, int posState)
    {
        ref ushort lenRoot = ref MemoryMarshal.GetArrayDataReference(lenProbs);

        if (rc.DecodeBit(ref lenRoot) == 0)
            return (int)rc.DecodeBitTree(ref Unsafe.Add(ref lenRoot, kLenLow + (posState << LzmaConstants.kNumLowLenBits)), LzmaConstants.kNumLowLenBits);
        if (rc.DecodeBit(ref Unsafe.Add(ref lenRoot, kLenChoice2)) == 0)
            return LzmaConstants.kNumLowLenSymbols + (int)rc.DecodeBitTree(ref Unsafe.Add(ref lenRoot, kLenMid + (posState << LzmaConstants.kNumMidLenBits)), LzmaConstants.kNumMidLenBits);

        return LzmaConstants.kNumLowLenSymbols + LzmaConstants.kNumMidLenSymbols + (int)rc.DecodeBitTree(ref Unsafe.Add(ref lenRoot, kLenHigh), LzmaConstants.kNumHighLenBits);
    }

    private int DecodeDistSlot(ref RangeDecoder rc, int lenToPosState) => (int)rc.DecodeBitTree(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_posSlotCoders), lenToPosState * LzmaConstants.kNumPosSlots), LzmaConstants.kNumPosSlotBits);

    private int DecodeDistance(ref RangeDecoder rc, int distSlot)
    {
        if (distSlot < LzmaConstants.kStartPosModelIndex)
            return distSlot;

        int numDirectBits = (distSlot >> 1) - 1;
        uint dist = (uint)((2 | (distSlot & 1)) << numDirectBits);

        if (distSlot < LzmaConstants.kEndPosModelIndex)
        {
            int offset = (int)dist - distSlot - 1;

            dist += rc.DecodeReverseBitTree(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_posSpecProbs), offset), numDirectBits);
        }
        else
        {
            dist += rc.DecodeDirectBits(numDirectBits - LzmaConstants.kNumAlignBits) << LzmaConstants.kNumAlignBits;
            dist += rc.DecodeReverseBitTree(ref MemoryMarshal.GetArrayDataReference(_alignProbs), LzmaConstants.kNumAlignBits);
        }

        return (int)dist;
    }
}