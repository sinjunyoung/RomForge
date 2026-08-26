using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

public sealed class Xd3LzmaDecoder
{
    private static readonly byte[] XzMagic = [0xFD, (byte)'7', (byte)'z', (byte)'X', (byte)'Z', 0x00];

    private bool _headerParsed;
    private LzmaDecoder? _lzmaDecoder;
    private int _lc, _lp, _pb;
    private bool _needProperties = true;
    private int _dictStart;
    private byte[] _output = new byte[4096];
    private int _totalOut;

    public byte[] Decode(byte[] section, ref int pos, int max, int decSize)
    {
        int p = pos;

        if (!_headerParsed)
        {
            p = ParseXzHeader(section, p, max);
            _headerParsed = true;
        }

        int startOut = _totalOut;
        int targetOut = _totalOut + decSize;

        EnsureCapacity(targetOut);
        DecodeLzma2Chunks(section, ref p, max, targetOut);

        if (_totalOut != targetOut)
            throw new Xd3Exception("secondary decoder short output");


        pos = p;

        var windowSlice = new byte[decSize];

        Array.Copy(_output, startOut, windowSlice, 0, decSize);

        return windowSlice;
    }

    private static int ParseXzHeader(byte[] section, int p, int max)
    {
        if (max - p < 12 || !section.AsSpan(p, 6).SequenceEqual(XzMagic))
            throw new Xd3Exception("not a valid XZ stream (lzma secondary section)");

        p += 12;

        if (p >= max)
            throw new Xd3Exception("truncated XZ block header");

        byte headerSizeByte = section[p];
        int blockHeaderRealSize = (headerSizeByte + 1) * 4;

        if (p + blockHeaderRealSize > max)
            throw new Xd3Exception("truncated XZ block header");

        byte blockFlags = section[p + 1];
        int numFilters = (blockFlags & 0x03) + 1;

        if ((blockFlags & 0xC0) != 0)
            throw new Xd3Exception("unsupported XZ block header flags (compressed/uncompressed size present)");

        if (numFilters != 1)
            throw new Xd3Exception("unsupported XZ block: expected exactly one filter (LZMA2)");

        int hp = p + 2;
        int filterId = section[hp++];

        if (filterId != 0x21)
            throw new Xd3Exception("unsupported XZ filter: expected LZMA2 (0x21)");

        int filterPropsSize = section[hp++];

        if (filterPropsSize != 1)
            throw new Xd3Exception("unexpected LZMA2 filter properties size");

        byte dictSizeByte = section[hp++];

        if (dictSizeByte > 40)
            throw new Xd3Exception("invalid LZMA2 dictionary size byte");

        return p + blockHeaderRealSize;
    }

    private void EnsureCapacity(int needed)
    {
        if (_output.Length < needed)
        {
            int newSize = Math.Max(needed, _output.Length * 2);

            Array.Resize(ref _output, newSize);
        }
    }

    private void DecodeLzma2Chunks(byte[] input, ref int inPos, int max, int targetOut)
    {
        while (inPos < max && _totalOut < targetOut)
        {
            byte control = input[inPos++];

            if (control == 0x00)
                break;

            if (control == 0x01 || control == 0x02)
            {
                if (control == 0x01)
                {
                    _dictStart = _totalOut;
                    _needProperties = true;
                }

                int dataSize = ((input[inPos] << 8) | input[inPos + 1]) + 1;

                inPos += 2;

                EnsureCapacity(_totalOut + dataSize);
                Array.Copy(input, inPos, _output, _totalOut, dataSize);

                _totalOut += dataSize;
                inPos += dataSize;

                continue;
            }

            if (control < 0x80)
                throw new Xd3Exception($"invalid LZMA2 control byte: 0x{control:X2}");

            bool resetDict = control >= 0xE0;
            bool resetState = control >= 0xA0;
            bool newProps = control >= 0xC0;
            int uncompSize = ((control & 0x1F) << 16) | (input[inPos] << 8) | input[inPos + 1];

            uncompSize++;
            inPos += 2;

            int compSize = (input[inPos] << 8) | input[inPos + 1];

            compSize++;
            inPos += 2;

            if (newProps)
            {
                byte propsByte = input[inPos++];

                if (!LzmaConstants.DecodeProperties(propsByte, out _lc, out _lp, out _pb))
                    throw new Xd3Exception("invalid LZMA properties byte");

                _needProperties = false;
            }

            if (_needProperties)
                throw new Xd3Exception("LZMA2 properties not set before data chunk");

            if (resetDict)
                _dictStart = _totalOut;

            if (_lzmaDecoder == null || (resetState && _lzmaDecoder.LcLp != _lc + _lp))
                _lzmaDecoder = new LzmaDecoder(_lc, _lp, _pb);
            else if (resetState)
            {
                _lzmaDecoder.SetProperties(_lc, _lp, _pb);
                _lzmaDecoder.ResetState();
            }

            EnsureCapacity(_totalOut + uncompSize);

            var rc = new RangeDecoder();

            rc.Init(input.AsSpan(inPos, compSize), 0);

            int outPos = _totalOut;

            _lzmaDecoder.DecodeChunk(ref rc, _output, ref outPos, _dictStart, uncompSize);
            _totalOut = outPos;
            inPos += compSize;
        }
    }
}