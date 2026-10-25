namespace Vita.Core.Cryptography;

internal sealed class PuffInflate
{
    private const int MaxBits = 15;
    private const int MaxLCodes = 286;
    private const int MaxDCodes = 30;
    private const int MaxCodes = MaxLCodes + MaxDCodes;
    private const int FixLCodes = 288;

    private sealed class Huffman
    {
        public short[] Count = new short[MaxBits + 1];

        public short[] Symbol = new short[FixLCodes];
    }

    private byte[]? _out;
    private int _outLen;
    private int _outCnt;
    private byte[] _in = [];
    private int _inLen;
    private int _inCnt;
    private int _bitBuf;
    private int _bitCnt;

    public int Puff(int dictLen, byte[]? dest, ref int destLen, byte[] source, ref int sourceLen)
    {
        _out = dest;
        _outLen = destLen;
        _outCnt = dictLen;
        _in = source;
        _inLen = sourceLen;
        _inCnt = 0;
        _bitBuf = 0;
        _bitCnt = 0;

        int err;

        try
        {
            bool last;

            do
            {
                last = Bits(1) != 0;

                int type = Bits(2);

                err = type switch
                {
                    0 => Stored(),
                    1 => Fixed(),
                    2 => Dynamic(),
                    _ => -1
                };

                if (err != 0)
                    break;
            } while (!last);
        }
        catch (PuffOutOfInputException)
        {
            err = 2;
        }

        if (err <= 0)
        {
            destLen = _outCnt - dictLen;
            sourceLen = _inCnt;
        }

        return err;
    }

    private int Bits(int need)
    {
        long val = _bitBuf;

        while (_bitCnt < need)
        {
            if (_inCnt == _inLen)
                throw new PuffOutOfInputException();

            val |= (long)_in[_inCnt++] << _bitCnt;
            _bitCnt += 8;
        }

        _bitBuf = (int)(val >> need);
        _bitCnt -= need;

        return (int)(val & ((1L << need) - 1));
    }

    private int Stored()
    {
        _bitBuf = 0;
        _bitCnt = 0;

        if (_inCnt + 4 > _inLen)
            return 2;

        int len = _in[_inCnt++];
        len |= _in[_inCnt++] << 8;

        if (_in[_inCnt++] != (~len & 0xff) || _in[_inCnt++] != ((~len >> 8) & 0xff))
            return -2;

        if (_inCnt + len > _inLen)
            return 2;

        if (_out != null)
        {
            if (_outCnt + len > _outLen)
                return 1;

            while (len-- > 0)
                _out[_outCnt++] = _in[_inCnt++];
        }
        else
        {
            _outCnt += len;
            _inCnt += len;
        }

        return 0;
    }

    private int Decode(Huffman h)
    {
        int code = 0, first = 0, index = 0, len = 1;
        int bitBuf = _bitBuf;
        int left = _bitCnt;
        int nextIdx = 1;

        while (true)
        {
            while (left-- > 0)
            {
                code |= bitBuf & 1;
                bitBuf >>= 1;

                int count = h.Count[nextIdx++];

                if (code - count < first)
                {
                    _bitBuf = bitBuf;
                    _bitCnt = (_bitCnt - len) & 7;

                    return h.Symbol[index + (code - first)];
                }

                index += count;
                first += count;
                first <<= 1;
                code <<= 1;
                len++;
            }

            left = (MaxBits + 1) - len;

            if (left == 0)
                break;

            if (_inCnt == _inLen)
                throw new PuffOutOfInputException();

            bitBuf = _in[_inCnt++];

            if (left > 8)
                left = 8;
        }

        return -10;
    }

    private static int Construct(Huffman h, short[] length, int n)
    {
        for (int len = 0; len <= MaxBits; len++)
            h.Count[len] = 0;

        for (int symbol = 0; symbol < n; symbol++)
            h.Count[length[symbol]]++;

        if (h.Count[0] == n)
            return 0;

        int left = 1;

        for (int len = 1; len <= MaxBits; len++)
        {
            left <<= 1;
            left -= h.Count[len];

            if (left < 0)
                return left;
        }

        var offs = new short[MaxBits + 1];
        offs[1] = 0;

        for (int len = 1; len < MaxBits; len++)
            offs[len + 1] = (short)(offs[len] + h.Count[len]);

        for (int symbol = 0; symbol < n; symbol++)
        {
            if (length[symbol] != 0)
                h.Symbol[offs[length[symbol]]++] = (short)symbol;
        }

        return left;
    }

    private static readonly short[] Lens = [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258];
    private static readonly short[] LExt = [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0];
    private static readonly short[] Dists = [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577];
    private static readonly short[] DExt = [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13];
    private static readonly short[] Order = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];

    private int Codes(Huffman lenCode, Huffman distCode)
    {
        int symbol;

        do
        {
            symbol = Decode(lenCode);

            if (symbol < 0)
                return symbol;

            if (symbol < 256)
            {
                if (_out != null)
                {
                    if (_outCnt == _outLen)
                        return 1;

                    _out[_outCnt] = (byte)symbol;
                }

                _outCnt++;
            }
            else if (symbol > 256)
            {
                symbol -= 257;

                if (symbol >= 29)
                    return -10;

                int len = Lens[symbol] + Bits(LExt[symbol]);

                symbol = Decode(distCode);

                if (symbol < 0)
                    return symbol;

                int dist = Dists[symbol] + Bits(DExt[symbol]);

                if (dist > _outCnt)
                    return -11;

                if (_out != null)
                {
                    if (_outCnt + len > _outLen)
                        return 1;

                    while (len-- > 0)
                    {
                        _out[_outCnt] = _out[_outCnt - dist];
                        _outCnt++;
                    }
                }
                else
                    _outCnt += len;
            }
        } while (symbol != 256);

        return 0;
    }

    private int Fixed()
    {
        var lenCode = new Huffman();
        var distCode = new Huffman();
        var lengths = new short[FixLCodes];
        int symbol = 0;

        for (; symbol < 144; symbol++)
            lengths[symbol] = 8;

        for (; symbol < 256; symbol++)
            lengths[symbol] = 9;

        for (; symbol < 280; symbol++)
            lengths[symbol] = 7;

        for (; symbol < FixLCodes; symbol++)
            lengths[symbol] = 8;

        Construct(lenCode, lengths, FixLCodes);

        var distLengths = new short[MaxDCodes];

        for (symbol = 0; symbol < MaxDCodes; symbol++)
            distLengths[symbol] = 5;

        Construct(distCode, distLengths, MaxDCodes);

        return Codes(lenCode, distCode);
    }

    private int Dynamic()
    {
        var lengths = new short[MaxCodes];
        var lenCode = new Huffman();
        var distCode = new Huffman();
        int nlen = Bits(5) + 257;
        int ndist = Bits(5) + 1;
        int ncode = Bits(4) + 4;

        if (nlen > MaxLCodes || ndist > MaxDCodes)
            return -3;

        int index = 0;

        for (; index < ncode; index++)
            lengths[Order[index]] = (short)Bits(3);

        for (; index < 19; index++)
            lengths[Order[index]] = 0;

        int err = Construct(lenCode, lengths, 19);

        if (err != 0)
            return -4;

        index = 0;

        while (index < nlen + ndist)
        {
            int symbol = Decode(lenCode);

            if (symbol < 0)
                return symbol;

            if (symbol < 16)
                lengths[index++] = (short)symbol;
            else
            {
                int len = 0;

                if (symbol == 16)
                {
                    if (index == 0)
                        return -5;

                    len = lengths[index - 1];
                    symbol = 3 + Bits(2);
                }
                else if (symbol == 17)
                    symbol = 3 + Bits(3);
                else
                    symbol = 11 + Bits(7);

                if (index + symbol > nlen + ndist)
                    return -6;

                while (symbol-- > 0)
                    lengths[index++] = (short)len;
            }
        }

        if (lengths[256] == 0)
            return -9;

        var litLengths = lengths[..nlen];
        err = Construct(lenCode, litLengths, nlen);

        if (err != 0 && (err < 0 || nlen != lenCode.Count[0] + lenCode.Count[1]))
            return -7;

        var distLengths = lengths[nlen..(nlen + ndist)];
        err = Construct(distCode, distLengths, ndist);

        if (err != 0 && (err < 0 || ndist != distCode.Count[0] + distCode.Count[1]))
            return -8;

        return Codes(lenCode, distCode);
    }
}