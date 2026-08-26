using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

public static class Xd3Decoder
{
    public static byte[] Decode(byte[] patch, Xd3Source? source, IXd3BlockSource? blockSource)
    {
        using var output = new MemoryStream();

        Decode(patch, source, blockSource, output);

        return output.ToArray();
    }

    public static void Decode(byte[] patch, Xd3Source? source, IXd3BlockSource? blockSource, Stream output, Action<long>? onProgress = null, CancellationToken ct = default)
    {
        int pos;

        if (patch.Length < 5 || patch[0] != Xd3Constants.VcdMagic1 || patch[1] != Xd3Constants.VcdMagic2 || patch[2] != Xd3Constants.VcdMagic3)
            throw new Xd3Exception("not a VCDIFF input");

        if (patch[3] != 0)
            throw new Xd3Exception("VCDIFF input version > 0 is not supported");

        pos = 4;

        byte hdrInd = patch[pos++];

        if ((hdrInd & Xd3Constants.VcdInvHdr) != 0)
            throw new Xd3Exception("unrecognized header indicator bits set");

        bool hasSecondary = (hdrInd & Xd3Constants.VcdSecondary) != 0;
        byte secondaryId = 0;

        if (hasSecondary)
            secondaryId = patch[pos++];

        if ((hdrInd & Xd3Constants.VcdCodeTable) != 0)
            throw new Xd3Exception("custom code table not supported");

        var codeTable = Xd3CodeTable.Rfc3284;
        var acache = new Xd3AddrCache { SNear = 4, SSame = 3 };

        Xd3AddrCacheOps.Alloc(acache);

        if ((hdrInd & Xd3Constants.VcdAppHeader) != 0)
        {
            uint appLen = Xd3VarInt.ReadSize(patch, ref pos, patch.Length);
            pos += (int)appLen;
        }

        Xd3FgkDecoder? dataFgk = null, instFgk = null, addrFgk = null;
        Xd3LzmaDecoder? dataLzma = null, instLzma = null, addrLzma = null;

        long totalWritten = 0;

        while (pos < patch.Length)
        {
            ct.ThrowIfCancellationRequested();

            byte winInd = patch[pos++];

            if ((winInd & Xd3Constants.VcdInvWin) != 0)
                throw new Xd3Exception("unrecognized window indicator bits set");

            Xd3AddrCacheOps.Init(acache);

            uint cpyLen = 0;
            ulong cpyOff = 0;
            bool hasSrc = (winInd & Xd3Constants.VcdSrcOrTgt) != 0;

            if (hasSrc)
            {
                if ((winInd & Xd3Constants.VcdTarget) != 0)
                    throw new Xd3Exception("VCD_TARGET not implemented");

                cpyLen = Xd3VarInt.ReadSize(patch, ref pos, patch.Length);
                cpyOff = Xd3VarInt.ReadOffset(patch, ref pos, patch.Length);

                if (source == null || blockSource == null)
                    throw new Xd3Exception("source input required");
            }

            uint encLen = Xd3VarInt.ReadSize(patch, ref pos, patch.Length);
            uint tgtLen = Xd3VarInt.ReadSize(patch, ref pos, patch.Length);

            byte delInd = patch[pos++];

            if ((delInd & Xd3Constants.VcdInvDel) != 0)
                throw new Xd3Exception("unrecognized delta indicator bits set");

            if (delInd != 0 && !hasSecondary)
                throw new Xd3Exception("invalid delta indicator bits set");

            bool dataComp = (delInd & Xd3Constants.VcdDataComp) != 0;
            bool instComp = (delInd & Xd3Constants.VcdInstComp) != 0;
            bool addrComp = (delInd & Xd3Constants.VcdAddrComp) != 0;
            uint dataLen = Xd3VarInt.ReadSize(patch, ref pos, patch.Length);
            uint instLen = Xd3VarInt.ReadSize(patch, ref pos, patch.Length);
            uint addrLen = Xd3VarInt.ReadSize(patch, ref pos, patch.Length);
            bool hasCksum = (winInd & Xd3Constants.VcdAdler32) != 0;
            uint declaredAdler = 0;

            if (hasCksum)
            {
                declaredAdler = (uint)(patch[pos] << 24 | patch[pos + 1] << 16 | patch[pos + 2] << 8 | patch[pos + 3]);
                pos += 4;
            }

            uint sizeofSum = Xd3VarInt.SizeofUint32(tgtLen) + Xd3VarInt.SizeofUint32(dataLen) + Xd3VarInt.SizeofUint32(instLen) + Xd3VarInt.SizeofUint32(addrLen);
            uint encLenCheck = 1 + sizeofSum + dataLen + instLen + addrLen + (hasCksum ? 4U : 0U);

            if (encLen != encLenCheck)
                throw new Xd3Exception("incorrect encoding length (redundant)");

            int dataStart = pos;
            int instStart = dataStart + (int)dataLen;
            int addrStart = instStart + (int)instLen;
            int sectionsEnd = addrStart + (int)addrLen;

            if (sectionsEnd > patch.Length)
                throw new Xd3Exception("further input required");

            if (hasSrc)
            {
                Xd3SourceOps.BlksizeDiv((long)cpyOff, source!, out long cpyOffBlocks, out uint cpyOffBlkOff);
                source!.CpyOffBlocks = cpyOffBlocks;
                source.CpyOffBlkOff = cpyOffBlkOff;
            }

            byte[] dataBuf = ExtractSection(patch, dataStart, (int)dataLen, dataComp, secondaryId, ref dataFgk, ref dataLzma);
            byte[] instBuf = ExtractSection(patch, instStart, (int)instLen, instComp, secondaryId, ref instFgk, ref instLzma);
            byte[] addrBuf = ExtractSection(patch, addrStart, (int)addrLen, addrComp, secondaryId, ref addrFgk, ref addrLzma);
            var targetWindow = new byte[tgtLen];
            int outPos = 0;
            int dataPos = 0;
            int instPos = 0;
            int addrPos = 0;
            uint decPosition = cpyLen;
            uint maxPos = cpyLen + tgtLen;

            while (instPos < instBuf.Length)
            {
                byte code = instBuf[instPos++];
                Xd3Dinst dinst = codeTable[code];

                ProcessHalf(dinst.Type1, dinst.Size1);
                ProcessHalf(dinst.Type2, dinst.Size2);
            }

            void ProcessHalf(byte type, byte sizeField)
            {
                if (type == Xd3Constants.Xd3Noop)
                    return;

                uint size = sizeField;

                if (size == 0)
                    size = Xd3VarInt.ReadSize(instBuf, ref instPos, instBuf.Length);

                uint here = decPosition;
                uint addr = 0;

                if (type >= Xd3Constants.Xd3Cpy)
                {
                    uint mode = (uint)(type - Xd3Constants.Xd3Cpy);

                    addr = Xd3AddrCacheOps.DecodeAddress(acache, here, mode, addrBuf, ref addrPos, addrBuf.Length);

                    if (addr >= here)
                        throw new Xd3Exception("address too large");

                    if (addr < cpyLen && addr + size > cpyLen)
                        throw new Xd3Exception("size too large");
                }

                if (decPosition + size > maxPos)
                    throw new Xd3Exception("size too large");

                decPosition += size;

                switch (type)
                {
                    case Xd3Constants.Xd3Run:
                        if (dataPos >= dataBuf.Length)
                            throw new Xd3Exception("data underflow");

                        byte fillByte = dataBuf[dataPos++];

                        for (int i = 0; i < size; i++)
                            targetWindow[outPos + i] = fillByte;

                        outPos += (int)size;

                        break;

                    case Xd3Constants.Xd3Add:
                        if (dataPos + size > dataBuf.Length)
                            throw new Xd3Exception("data underflow");

                        Array.Copy(dataBuf, dataPos, targetWindow, outPos, (int)size);

                        dataPos += (int)size;
                        outPos += (int)size;

                        break;

                    default:
                        if (addr < cpyLen)
                            CopyFromSource(source!, blockSource!, cpyOff + addr, size, targetWindow, outPos);
                        else
                        {
                            uint srcPos = addr - cpyLen;

                            for (int i = 0; i < size; i++)
                                targetWindow[outPos + i] = targetWindow[(int)srcPos + i];
                        }

                        outPos += (int)size;

                        break;
                }
            }

            if (outPos != tgtLen)
                throw new Xd3Exception("wrong window length");

            if (dataPos != dataBuf.Length)
                throw new Xd3Exception("extra data section");

            if (addrPos != addrBuf.Length)
                throw new Xd3Exception("extra address section");

            if (hasCksum)
            {
                uint actual = Adler32.Compute(1, targetWindow);

                if (actual != declaredAdler)
                    throw new Xd3Exception("target window checksum mismatch: the supplied source likely does not match the one used to create this patch");
            }

            output.Write(targetWindow, 0, targetWindow.Length);

            totalWritten += targetWindow.Length;

            onProgress?.Invoke(totalWritten);

            pos = sectionsEnd;
        }
    }

    private static byte[] ExtractSection(byte[] patch, int start, int length, bool compressed, byte secondaryId, ref Xd3FgkDecoder? fgk, ref Xd3LzmaDecoder? lzma)
    {
        var raw = new byte[length];

        Array.Copy(patch, start, raw, 0, length);

        if (!compressed)
            return raw;

        return DecodeSecondarySection(raw, secondaryId, ref fgk, ref lzma);
    }

    private static byte[] DecodeSecondarySection(byte[] section, byte secondaryId, ref Xd3FgkDecoder? fgk, ref Xd3LzmaDecoder? lzma)
    {
        int pos = 0;
        uint decSize = Xd3VarInt.ReadSize(section, ref pos, section.Length);

        if (decSize == 0)
            throw new Xd3Exception("secondary decoder invalid output size");

        byte[] result;

        switch (secondaryId)
        {
            case (byte)Xd3SecondaryId.Djw:
                result = Xd3DjwDecoder.DecodeHuff(section, ref pos, section.Length, (int)decSize);
                break;
            case (byte)Xd3SecondaryId.Fgk:
                fgk ??= new Xd3FgkDecoder();
                result = fgk.Decode(section, ref pos, section.Length, (int)decSize);
                break;
            case (byte)Xd3SecondaryId.Lzma:
                lzma ??= new Xd3LzmaDecoder();
                result = lzma.Decode(section, ref pos, section.Length, (int)decSize);
                break;
            default:
                throw new Xd3Exception("unknown secondary compressor ID");
        }

        if (pos != section.Length)
            throw new Xd3Exception("secondary decoder finished with unused input");

        return result;
    }

    private static void CopyFromSource(Xd3Source source, IXd3BlockSource blockSource, ulong absoluteOffset, uint size, byte[] dest, int destPos)
    {
        if (blockSource is IXd3RandomAccessSource ras)
        {
            ras.ReadAt((long)absoluteOffset, dest, destPos, (int)size);
            return;
        }

        Xd3SourceOps.BlksizeDiv((long)absoluteOffset, source, out long block, out uint blkoff);

        uint remaining = size;
        int destOffset = destPos;

        while (remaining > 0)
        {
            Xd3SourceOps.GetBlk(source, blockSource, block);

            uint onBlk = Xd3SourceOps.BytesOnSrcBlk(source, block);

            if (blkoff >= onBlk)
                throw new Xd3Exception("source file too short");

            uint available = onBlk - blkoff;
            uint take = Math.Min(remaining, available);

            Array.Copy(source.CurBlk!, (int)blkoff, dest, destOffset, (int)take);

            destOffset += (int)take;
            remaining -= take;
            block += 1;
            blkoff = 0;
        }
    }
}