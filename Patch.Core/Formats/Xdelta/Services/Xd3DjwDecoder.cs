using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

public static class Xd3DjwDecoder
{
    private const uint DjwMaxCodelen = 20;
    private const uint DjwTotalCodes = DjwMaxCodelen + 2;
    private const uint Run0 = 0;
    private const uint Run1 = 1;
    private const uint DjwBasicCodes = 5;
    private const uint DjwRunCodes = 2;
    private const uint DjwExtra12Offset = DjwBasicCodes + DjwRunCodes;
    private const uint DjwExtraCodes = 15;
    private const uint DjwExtraCodeBits = 4;
    private const uint DjwMaxGroups = 8;
    private const uint DjwGroupBits = 3;
    private const uint DjwSectorszMult = 5;
    private const uint DjwSectorszBits = 5;
    private const uint DjwMaxClclen = 15;
    private const uint DjwClclenBits = 4;
    private const uint DjwMaxGbclen = 7;
    private const uint DjwGbclenBits = 3;
    private const uint AlphabetSize = 256;

    private static readonly byte[] Encode12Extra = [9, 10, 3, 11, 2, 12, 13, 1, 14, 15, 16, 17, 18, 19, 20];
    private static readonly byte[] Encode12Basic = [4, 5, 6, 7, 8];

    public static uint DecodeBits(byte[] buf, ref int pos, int max, ref DjwBitState bits, uint nbits)
    {
        uint value = 0;
        uint vmask = 1u << (int)nbits;
        bool needByte = bits.CurMask == 0x100;

        while (true)
        {
            if (!needByte)
            {
                do
                {
                    vmask >>= 1;

                    if ((bits.CurByte & bits.CurMask) != 0)
                        value |= vmask;

                    bits.CurMask <<= 1;

                    if (vmask == 1)
                        return value;

                } while (bits.CurMask != 0x100);
            }

            if (pos == max)
                throw new Xd3Exception("secondary decoder end of input");

            bits.CurByte = buf[pos++];
            bits.CurMask = 1;
            needByte = false;
        }
    }

    private static uint DecodeSymbol(byte[] buf, ref int pos, int max, ref DjwBitState bits, byte[] inorder, uint[] baseArr, uint[] limitArr, uint minClen, uint maxClen, uint maxSym)
    {
        uint code = 0;
        uint bitCount = 0;
        bool needByte = bits.CurMask == 0x100;

        while (true)
        {
            if (!needByte)
            {
                do
                {
                    if (bitCount == maxClen)
                        throw new Xd3Exception("secondary decoder invalid code");

                    bitCount += 1;
                    code <<= 1;

                    if ((bits.CurByte & bits.CurMask) != 0)
                        code |= 1;

                    bits.CurMask <<= 1;

                    if (bitCount >= minClen && code <= limitArr[bitCount])
                        goto done;
                } while (bits.CurMask != 0x100);
            }

            if (pos == max)
                throw new Xd3Exception("secondary decoder end of input");

            bits.CurByte = buf[pos++];
            bits.CurMask = 1;
            needByte = false;
        }

        done:
        if (baseArr[bitCount] <= code)
        {
            uint offset = code - baseArr[bitCount];

            if (offset <= maxSym)
                return inorder[offset];
        }

        throw new Xd3Exception("secondary decoder invalid code");
    }

    private static void BuildDecoder(uint asize, uint absMax, byte[] clen, byte[] inorder, uint[] baseArr, uint[] limitArr, out uint minClenOut, out uint maxClenOut)
    {
        var nrClen = new uint[DjwTotalCodes];
        var tmpBase = new uint[DjwTotalCodes];

        for (uint i = 0; i < asize; i++)
            nrClen[clen[i]]++;

        uint minClen = 1;

        while (minClen <= absMax && nrClen[minClen] == 0)
            minClen++;

        uint maxClen = absMax;

        while (maxClen != 0 && nrClen[maxClen] == 0)
            maxClen--;

        tmpBase[minClen] = 0;
        baseArr[minClen] = 0;
        limitArr[minClen] = nrClen[minClen] - 1;

        for (uint i = minClen + 1; i <= maxClen; i++)
        {
            uint lastLimit = (limitArr[i - 1] + 1) << 1;

            tmpBase[i] = tmpBase[i - 1] + nrClen[i - 1];
            limitArr[i] = lastLimit + nrClen[i] - 1;
            baseArr[i] = lastLimit - tmpBase[i];
        }

        for (uint i = 0; i < asize; i++)
        {
            byte l = clen[i];

            if (l != 0)
                inorder[tmpBase[l]++] = (byte)i;
        }

        minClenOut = minClen;
        maxClenOut = maxClen;
    }

    private static uint UpdateMtf(byte[] mtf, uint mtfI)
    {
        uint sym = mtf[mtfI];

        for (int k = (int)mtfI; k != 0; k--)
            mtf[k] = mtf[k - 1];

        mtf[0] = (byte)sym;

        return sym;
    }

    private static void InitClenMtf12(byte[] clmtf)
    {
        int i = 0;

        clmtf[i++] = 0;

        foreach (byte b in Encode12Basic)
            clmtf[i++] = b;

        foreach (byte b in Encode12Extra)
            clmtf[i++] = b;
    }

    private static void DecodeClclen(byte[] buf, ref int pos, int max, ref DjwBitState bits, byte[] clInorder, uint[] clBase, uint[] clLimit, out uint clMinLen, out uint clMaxLen, byte[] clMtf)
    {
        uint numCodes = DecodeBits(buf, ref pos, max, ref bits, DjwExtraCodeBits);

        numCodes += DjwExtra12Offset;

        var clClen = new byte[DjwTotalCodes];
        uint i;

        for (i = 0; i < numCodes; i++)
        {
            uint value = DecodeBits(buf, ref pos, max, ref bits, DjwClclenBits);

            clClen[i] = (byte)value;
        }

        for (; i < DjwTotalCodes; i++)
            clClen[i] = 0;

        BuildDecoder(DjwTotalCodes, DjwMaxClclen, clClen, clInorder, clBase, clLimit, out clMinLen, out clMaxLen);
        InitClenMtf12(clMtf);
    }

    private static void Decode12(byte[] buf, ref int pos, int max, ref DjwBitState bits, byte[] inorder, uint[] baseArr, uint[] limitArr, uint minlen, uint maxlen, byte[] mtfvals, uint elts, uint skipOffset, byte[] values)
    {
        uint n = 0, rep = 0, mtf = 0, s = 0;

        while (n < elts)
        {
            if (skipOffset != 0 && n >= skipOffset && values[n - skipOffset] == 0)
            {
                values[n++] = 0;

                continue;
            }

            if (rep != 0)
            {
                values[n++] = mtfvals[0];
                rep -= 1;

                continue;
            }

            if (mtf != 0)
            {
                uint sym = UpdateMtf(mtfvals, mtf);

                values[n++] = (byte)sym;
                mtf = 0;

                continue;
            }

            mtf = DecodeSymbol(buf, ref pos, max, ref bits, inorder, baseArr, limitArr, minlen, maxlen, DjwTotalCodes);

            if (mtf <= Run1)
            {
                rep = (mtf + 1) << (int)s;
                mtf = 0;
                s += 1;
            }
            else
            {
                mtf -= 1;
                s = 0;
            }
        }

        if (rep != 0)
            throw new Xd3Exception("secondary decoder invalid repeat code");
    }

    private static void DecodePrefix(byte[] buf, ref int pos, int max, ref DjwBitState bits, byte[] clInorder, uint[] clBase, uint[] clLimit, uint clMinlen, uint clMaxlen, byte[] clMtf, uint groups, byte[] clen)
        => Decode12(buf, ref pos, max, ref bits, clInorder, clBase, clLimit, clMinlen, clMaxlen, clMtf, AlphabetSize * groups, AlphabetSize, clen);

    public static byte[] DecodeHuff(byte[] input, ref int inputPos, int inputEnd, int outputLen)
    {
        var bits = DjwBitState.DecodeInit();
        var output = new byte[outputLen];
        int outPos = 0;
        uint outputBytes = (uint)outputLen;

        if (outputBytes == 0)
            throw new Xd3Exception("secondary decoder invalid input");

        uint groups = DecodeBits(input, ref inputPos, inputEnd, ref bits, DjwGroupBits) + 1;

        uint sectorSize;

        if (groups > 1)
        {
            uint ss = DecodeBits(input, ref inputPos, inputEnd, ref bits, DjwSectorszBits);
            sectorSize = (ss + 1) * DjwSectorszMult;
        }
        else
            sectorSize = outputBytes;

        uint sectors = 1 + (outputBytes - 1) / sectorSize;
        var inorder = new byte[DjwMaxGroups][];
        var baseArr = new uint[DjwMaxGroups][];
        var limitArr = new uint[DjwMaxGroups][];
        var minlen = new uint[DjwMaxGroups];
        var maxlen = new uint[DjwMaxGroups];

        for (int g = 0; g < DjwMaxGroups; g++)
        {
            inorder[g] = new byte[AlphabetSize];
            baseArr[g] = new uint[DjwTotalCodes];
            limitArr[g] = new uint[DjwTotalCodes];
        }

        var clen = new byte[DjwMaxGroups][];

        for (int g = 0; g < DjwMaxGroups; g++)
            clen[g] = new byte[AlphabetSize];

        var clInorder = new byte[DjwTotalCodes];
        var clBase = new uint[DjwMaxClclen + 2];
        var clLimit = new uint[DjwMaxClclen + 2];
        var clMtf = new byte[DjwTotalCodes];

        DecodeClclen(input, ref inputPos, inputEnd, ref bits, clInorder, clBase, clLimit, out uint clMinlen, out uint clMaxlen, clMtf);

        var clenFlat = new byte[AlphabetSize * DjwMaxGroups];

        DecodePrefix(input, ref inputPos, inputEnd, ref bits, clInorder, clBase, clLimit, clMinlen, clMaxlen, clMtf, groups, clenFlat);

        for (int gp = 0; gp < groups; gp++)
            Array.Copy(clenFlat, gp * (int)AlphabetSize, clen[gp], 0, (int)AlphabetSize);

        for (int gp = 0; gp < groups; gp++)
            BuildDecoder(AlphabetSize, DjwMaxCodelen, clen[gp], inorder[gp], baseArr[gp], limitArr[gp], out minlen[gp], out maxlen[gp]);

        byte[]? selGroup = null;

        if (groups > 1)
        {
            var selClen = new byte[DjwMaxGroups + 1];
            var selMtf = new byte[DjwMaxGroups + 2];

            for (uint gp = 0; gp < groups + 1; gp++)
            {
                uint value = DecodeBits(input, ref inputPos, inputEnd, ref bits, DjwGbclenBits);
                selClen[gp] = (byte)value;
                selMtf[gp] = (byte)gp;
            }

            selGroup = new byte[sectors];

            var selInorder = new byte[DjwMaxGroups + 2];
            var selBase = new uint[DjwMaxGbclen + 2];
            var selLimit = new uint[DjwMaxGbclen + 2];

            BuildDecoder(groups + 1, DjwMaxGbclen, selClen, selInorder, selBase, selLimit, out uint selMinlen, out uint selMaxlen);
            Decode12(input, ref inputPos, inputEnd, ref bits, selInorder, selBase, selLimit, selMinlen, selMaxlen, selMtf, sectors, 0, selGroup);
        }

        byte[] gpInorder = inorder[0];
        uint[] gpBase = baseArr[0];
        uint[] gpLimit = limitArr[0];
        uint gpMinlen = minlen[0];
        uint gpMaxlen = maxlen[0];

        for (uint c = 0; c < sectors; c++)
        {
            if (groups >= 2)
            {
                uint gp = selGroup![c];

                gpInorder = inorder[gp];
                gpBase = baseArr[gp];
                gpLimit = limitArr[gp];
                gpMinlen = minlen[gp];
                gpMaxlen = maxlen[gp];
            }

            uint n = (uint)Math.Min(sectorSize, (uint)(outputLen - outPos));

            do
            {
                uint sym = DecodeSymbol(input, ref inputPos, inputEnd, ref bits, gpInorder, gpBase, gpLimit, gpMinlen, gpMaxlen, AlphabetSize);

                output[outPos++] = (byte)sym;
                n -= 1;
            } while (n != 0);
        }

        return output;
    }
}