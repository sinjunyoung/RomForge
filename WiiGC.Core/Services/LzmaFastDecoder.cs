using System.Runtime.CompilerServices;

namespace WiiGC.Core.Services;

internal sealed unsafe class LzmaFastDecoder
{
    private const int NumStates = 12;
    private const int IsMatch = 0;
    private const int IsRep = IsMatch + (NumStates << 4);
    private const int IsRepG0 = IsRep + NumStates;
    private const int IsRepG1 = IsRepG0 + NumStates;
    private const int IsRepG2 = IsRepG1 + NumStates;
    private const int IsRep0Long = IsRepG2 + NumStates;
    private const int PosSlot = IsRep0Long + (NumStates << 4);
    private const int SpecPos = PosSlot + (4 << 6);
    private const int Align = SpecPos + 128 - 14;
    private const int LenCoder = Align + 16;
    private const int RepLenCoder = LenCoder + 514;
    private const int Literal = RepLenCoder + 514;

    private const int LenChoice = 0;
    private const int LenChoice2 = 1;
    private const int LenLow = 2;
    private const int LenMid = LenLow + (16 << 3);
    private const int LenHigh = LenMid + (16 << 3);

    private const uint TopValue = 1u << 24;
    private const int InputPadding = 64;

    private readonly int _lc;
    private readonly int _lp;
    private readonly int _pb;
    private readonly ushort[] _probs;
    private byte[] _input = GC.AllocateUninitializedArray<byte>(1 << 16, pinned: true);

    public LzmaFastDecoder(ReadOnlySpan<byte> props)
    {
        if (props.Length < 5)
            throw new InvalidDataException("LZMA1 속성 데이터 크기가 올바르지 않습니다.");

        int d = props[0];

        if (d >= 9 * 5 * 5)
            throw new InvalidDataException("LZMA1 속성이 올바르지 않습니다.");

        _lc = d % 9;
        d /= 9;
        _lp = d % 5;
        _pb = d / 5;

        _probs = GC.AllocateUninitializedArray<ushort>(Literal + (0x300 << (_lc + _lp)), pinned: true);
    }

    public int Decode(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length < 5)
            throw new InvalidDataException("LZMA 데이터가 너무 짧습니다.");

        if (_input.Length < source.Length + InputPadding)
            _input = GC.AllocateUninitializedArray<byte>(source.Length + InputPadding, pinned: true);

        source.CopyTo(_input);
        _input.AsSpan(source.Length, InputPadding).Clear();

        _probs.AsSpan().Fill(1024);

        fixed (ushort* probs = _probs)
        fixed (byte* input = _input)
        fixed (byte* output = destination)
        {
            return DecodeCore(probs, input, input + source.Length, output, destination.Length, _lc, _lp, _pb);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint TreeStep(ushort* p, uint symbol, ref uint range, ref uint code, ref byte* buf)
    {
        uint ttt = *p;

        if (range < TopValue)
        {
            range <<= 8;
            code = (code << 8) | *buf++;
        }

        uint bound = (range >> 11) * ttt;
        uint mask = (uint)((long)((ulong)code - bound) >> 63);
        uint inv = ~mask;

        range = (bound & mask) | ((range - bound) & inv);
        code -= bound & inv;
        *p = (ushort)(ttt + (((2048 - ttt) >> 5) & mask) - ((ttt >> 5) & inv));

        return (symbol << 1) | (inv & 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Bit(ushort* p, ref uint range, ref uint code, ref byte* buf)
    {
        uint ttt = *p;

        if (range < TopValue)
        {
            range <<= 8;
            code = (code << 8) | *buf++;
        }

        uint bound = (range >> 11) * ttt;

        if (code < bound)
        {
            range = bound;
            *p = (ushort)(ttt + ((2048 - ttt) >> 5));
            return 0;
        }

        range -= bound;
        code -= bound;
        *p = (ushort)(ttt - (ttt >> 5));
        return 1;
    }

    private static int DecodeCore(ushort* probs, byte* buf, byte* bufEnd, byte* dst, int dstLength, int lc, int lp, int pb)
    {
        if (buf[0] != 0)
            throw new InvalidDataException("LZMA 스트림 헤더가 올바르지 않습니다.");

        uint range = 0xFFFFFFFF;
        uint code = ((uint)buf[1] << 24) | ((uint)buf[2] << 16) | ((uint)buf[3] << 8) | buf[4];
        buf += 5;

        uint lpMask = (1u << lp) - 1;
        uint pbMask = (1u << pb) - 1;
        int lcShift = 8 - lc;

        uint state = 0;
        uint rep0 = 1, rep1 = 1, rep2 = 1, rep3 = 1;
        int pos = 0;

        while (pos < dstLength)
        {
            if (buf > bufEnd)
                throw new InvalidDataException("LZMA 데이터가 잘렸습니다.");

            uint posState = (uint)pos & pbMask;

            if (Bit(probs + IsMatch + (state << 4) + posState, ref range, ref code, ref buf) == 0)
            {
                uint prev = pos > 0 ? dst[pos - 1] : 0u;
                ushort* lit = probs + Literal + 0x300 * (((((uint)pos & lpMask) << lc)) + (prev >> lcShift));
                uint symbol = 1;

                if (state < 7)
                {
                    do
                        symbol = TreeStep(lit + symbol, symbol, ref range, ref code, ref buf);
                    while (symbol < 0x100);
                }
                else
                {
                    uint matchByte = dst[pos - rep0];
                    uint offs = 0x100;

                    do
                    {
                        matchByte <<= 1;
                        uint bit = matchByte & offs;
                        ushort* p = lit + offs + bit + symbol;
                        uint ttt = *p;

                        if (range < TopValue)
                        {
                            range <<= 8;
                            code = (code << 8) | *buf++;
                        }

                        uint bound = (range >> 11) * ttt;
                        uint mask = (uint)((long)((ulong)code - bound) >> 63);
                        uint inv = ~mask;

                        range = (bound & mask) | ((range - bound) & inv);
                        code -= bound & inv;
                        *p = (ushort)(ttt + (((2048 - ttt) >> 5) & mask) - ((ttt >> 5) & inv));

                        symbol = (symbol << 1) | (inv & 1);
                        offs &= (~bit & mask) | (bit & inv);
                    }
                    while (symbol < 0x100);
                }

                dst[pos++] = (byte)symbol;
                state = state < 4 ? 0 : state < 10 ? state - 3 : state - 6;
                continue;
            }

            ushort* lenProbs;
            bool isRep;

            if (Bit(probs + IsRep + state, ref range, ref code, ref buf) == 0)
            {
                isRep = false;
                lenProbs = probs + LenCoder;
            }
            else
            {
                if (pos == 0)
                    throw new InvalidDataException("LZMA 데이터가 올바르지 않습니다.");

                isRep = true;

                if (Bit(probs + IsRepG0 + state, ref range, ref code, ref buf) == 0)
                {
                    if (Bit(probs + IsRep0Long + (state << 4) + posState, ref range, ref code, ref buf) == 0)
                    {
                        dst[pos] = dst[pos - rep0];
                        pos++;
                        state = state < 7 ? 9u : 11u;
                        continue;
                    }
                }
                else
                {
                    uint distance;

                    if (Bit(probs + IsRepG1 + state, ref range, ref code, ref buf) == 0)
                        distance = rep1;
                    else
                    {
                        if (Bit(probs + IsRepG2 + state, ref range, ref code, ref buf) == 0)
                            distance = rep2;
                        else
                        {
                            distance = rep3;
                            rep3 = rep2;
                        }

                        rep2 = rep1;
                    }

                    rep1 = rep0;
                    rep0 = distance;
                }

                state = state < 7 ? 8u : 11u;
                lenProbs = probs + RepLenCoder;
            }

            uint len;
            {
                ushort* probLen;
                uint offset;
                uint limit;

                if (Bit(lenProbs + LenChoice, ref range, ref code, ref buf) == 0)
                {
                    probLen = lenProbs + LenLow + (posState << 3);
                    offset = 0;
                    limit = 8;
                }
                else if (Bit(lenProbs + LenChoice2, ref range, ref code, ref buf) == 0)
                {
                    probLen = lenProbs + LenMid + (posState << 3);
                    offset = 8;
                    limit = 8;
                }
                else
                {
                    probLen = lenProbs + LenHigh;
                    offset = 16;
                    limit = 256;
                }

                len = 1;

                do
                    len = TreeStep(probLen + len, len, ref range, ref code, ref buf);
                while (len < limit);

                len = len - limit + offset;
            }

            if (!isRep)
            {
                ushort* slotProbs = probs + PosSlot + ((len < 4 ? len : 3u) << 6);
                uint slot = 1;

                for (int i = 0; i < 6; i++)
                    slot = TreeStep(slotProbs + slot, slot, ref range, ref code, ref buf);

                slot -= 64;

                uint distance = slot;

                if (slot >= 4)
                {
                    int numDirectBits = (int)((slot >> 1) - 1);

                    distance = 2 | (slot & 1);

                    if (slot < 14)
                    {
                        distance <<= numDirectBits;

                        ushort* spec = probs + SpecPos + distance - slot - 1;
                        uint m = 1;
                        uint mask = 1;

                        do
                        {
                            uint bit = Bit(spec + m, ref range, ref code, ref buf);

                            m = (m << 1) | bit;
                            distance |= mask & (0u - bit);
                            mask <<= 1;
                        }
                        while (--numDirectBits != 0);
                    }
                    else
                    {
                        numDirectBits -= 4;

                        do
                        {
                            if (range < TopValue)
                            {
                                range <<= 8;
                                code = (code << 8) | *buf++;
                            }

                            range >>= 1;
                            code -= range;

                            uint t = 0u - (code >> 31);

                            distance = (distance << 1) + (t + 1);
                            code += range & t;
                        }
                        while (--numDirectBits != 0);

                        distance <<= 4;

                        ushort* align = probs + Align;
                        uint m = 1;

                        for (int i = 0; i < 4; i++)
                        {
                            uint bit = Bit(align + m, ref range, ref code, ref buf);

                            m = (m << 1) | bit;
                            distance |= bit << i;
                        }

                        if (distance == 0xFFFFFFFF)
                            return pos;
                    }
                }

                if (distance >= (uint)pos)
                    throw new InvalidDataException("LZMA 거리 값이 올바르지 않습니다.");

                rep3 = rep2;
                rep2 = rep1;
                rep1 = rep0;
                rep0 = distance + 1;

                state = state < 7 ? 7u : 10u;
            }

            int length = (int)len + 2;
            int remain = dstLength - pos;

            if (length > remain)
                length = remain;

            byte* d = dst + pos;
            byte* s = d - rep0;

            pos += length;

            if (rep0 >= 8)
            {
                while (length >= 8)
                {
                    *(ulong*)d = *(ulong*)s;
                    d += 8;
                    s += 8;
                    length -= 8;
                }
            }

            while (length > 0)
            {
                *d++ = *s++;
                length--;
            }
        }

        return pos;
    }
}