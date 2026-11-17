using Common.Localization;
using Common.Models;

namespace Patch.Core.Formats;

public static class Bps
{
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    public static async Task ApplyPatchAsync(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, patchPath);

        byte[] source = await File.ReadAllBytesAsync(sourcePath, ct);
        byte[] patch = await File.ReadAllBytesAsync(patchPath, ct);
        byte[] result = await Task.Run(() => Decode(source, patch, progress, ct), ct);

        await File.WriteAllBytesAsync(outputPath, result, ct);
    }

    public static Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) => Task.Run(() => Decode(sourceData, patchData, progress, ct), ct);

    private unsafe static byte[] Decode(byte[] source, byte[] patch, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (patch.Length < 12)
            throw new InvalidDataException(Loc.S.ErrPatchFileTooShort("BPS"));

        fixed (byte* pSrc = source, pPat = patch)
        {
            if (pPat[0] != 'B' || pPat[1] != 'P' || pPat[2] != 'S' || pPat[3] != '1')
                throw new InvalidDataException(Loc.S.ErrInvalidPatchHeader("BPS"));

            int pos = 4;
            long sourceSize = ReadVli(pPat, ref pos, patch.Length);
            long targetSize = ReadVli(pPat, ref pos, patch.Length);
            long metadataSize = ReadVli(pPat, ref pos, patch.Length);

            if (source.LongLength != sourceSize)
                throw new InvalidDataException(Loc.S.ErrSourceSizeMismatch(sourceSize.ToString("N0"), source.LongLength.ToString("N0")));

            uint expectedSourceCrc = BitConverter.ToUInt32(patch, patch.Length - 12);
            uint actualSourceCrc = CalculateCrc32(source);

            if (actualSourceCrc != expectedSourceCrc)
                throw new InvalidDataException(Loc.S.ErrSourceCrc32Mismatch(expectedSourceCrc.ToString("X8"), actualSourceCrc.ToString("X8")));

            if (metadataSize < 0 || pos + metadataSize > patch.Length - 12)
                throw new InvalidDataException(Loc.S.ErrBpsMetadataSizeInvalid);

            pos += (int)metadataSize;

            byte[] target = new byte[targetSize];
            int patchEnd = patch.Length - 12;

            fixed (byte* pTar = target)
            {
                int outOffset = 0;
                int srcRelOffset = 0;
                int tarRelOffset = 0;

                while (pos < patchEnd)
                {
                    ct.ThrowIfCancellationRequested();

                    long data = ReadVli(pPat, ref pos, patchEnd);
                    int command = (int)(data & 3);
                    int length = (int)((data >> 2) + 1);

                    if (length < 0 || outOffset + (long)length > targetSize)
                        throw new InvalidDataException(Loc.S.ErrBpsCommandLengthOutOfRange);

                    switch (command)
                    {
                        case 0:
                            if (outOffset + (long)length > sourceSize)
                                throw new InvalidDataException(Loc.S.ErrBpsSourceReadOutOfRange);

                            Buffer.MemoryCopy(pSrc + outOffset, pTar + outOffset, targetSize - outOffset, length);

                            outOffset += length;

                            break;

                        case 1:
                            if (pos + (long)length > patchEnd)
                                throw new InvalidDataException(Loc.S.ErrBpsTargetReadOutOfRange);

                            Buffer.MemoryCopy(pPat + pos, pTar + outOffset, targetSize - outOffset, length);

                            pos += length;
                            outOffset += length;

                            break;

                        case 2:
                            long od2 = ReadVli(pPat, ref pos, patchEnd);

                            srcRelOffset += (od2 & 1) == 1 ? -(int)(od2 >> 1) : (int)(od2 >> 1);

                            if (srcRelOffset < 0 || srcRelOffset + (long)length > sourceSize)
                                throw new InvalidDataException(Loc.S.ErrBpsSourceCopyOutOfRange);

                            Buffer.MemoryCopy(pSrc + srcRelOffset, pTar + outOffset, targetSize - outOffset, length);

                            outOffset += length;
                            srcRelOffset += length;

                            break;

                        case 3:
                            long od3 = ReadVli(pPat, ref pos, patchEnd);

                            tarRelOffset += (od3 & 1) == 1 ? -(int)(od3 >> 1) : (int)(od3 >> 1);

                            if (tarRelOffset < 0 || tarRelOffset >= outOffset)
                                throw new InvalidDataException(Loc.S.ErrBpsTargetCopyInvalid);

                            for (int i = 0; i < length; i++)
                                pTar[outOffset + i] = pTar[tarRelOffset + i];

                            outOffset += length;
                            tarRelOffset += length;

                            break;
                    }

                    if (progress != null && pos % Math.Max(1, patchEnd / 100) == 0)
                        progress.Report(new ProgressInfo { Percent = (int)(pos / (double)patchEnd * 100) });
                }

                if (outOffset != targetSize)
                    throw new InvalidDataException(Loc.S.ErrBpsResultSizeMismatch);
            }

            return target;
        }
    }

    private unsafe static long ReadVli(byte* patch, ref int pos, int maxLen)
    {
        long value = 0, shift = 1;

        while (pos < maxLen)
        {
            byte b = patch[pos++];

            value += (b & 0x7F) * shift;

            if ((b & 0x80) != 0)
                break;

            shift <<= 7;
            value += shift;
        }

        return value;
    }

    private static uint[] BuildCrc32Table()
    {
        uint[] table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint value = i;

            for (int j = 0; j < 8; j++)
                value = (value & 1) != 0 ? (value >> 1) ^ 0xEDB88320 : value >> 1;

            table[i] = value;
        }

        return table;
    }

    private unsafe static uint CalculateCrc32(byte[] data, int length = -1)
    {
        if (length == -1) length = data.Length;

        uint crc = 0xFFFFFFFF;

        fixed (byte* p = data)
        fixed (uint* t = Crc32Table)
            for (int i = 0; i < length; i++)
                crc = t[(crc ^ p[i]) & 0xFF] ^ (crc >> 8);

        return ~crc;
    }

    private static void ValidateInputFiles(params string[] paths)
    {
        foreach (var path in paths)
            if (!File.Exists(path))
                throw new FileNotFoundException(Loc.S.ErrFileNotFoundName(path));
    }
}