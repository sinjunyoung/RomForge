using Common.Localization;
using Common.Models;

namespace Patch.Core.Formats;

public static class Ups
{
    private static readonly byte[] HeaderBytes = [(byte)'U', (byte)'P', (byte)'S', (byte)'1'];

    public static async Task ApplyPatchAsync(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, patchPath);

        byte[] input = await File.ReadAllBytesAsync(sourcePath, ct);
        byte[] patch = await File.ReadAllBytesAsync(patchPath, ct);
        byte[] result = await Task.Run(() => Decode(input, patch, progress, ct), ct);

        await File.WriteAllBytesAsync(outputPath, result, ct);
    }

    public static Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) => Task.Run(() => Decode(sourceData, patchData, progress, ct), ct);

    private unsafe static byte[] Decode(byte[] input, byte[] patch, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (patch.Length < 12) throw new InvalidDataException(Loc.S.ErrPatchFileTooShort("UPS"));

        fixed (byte* pPat = patch)
        {
            if (pPat[0] != 'U' || pPat[1] != 'P' || pPat[2] != 'S' || pPat[3] != '1')
                throw new InvalidDataException(Loc.S.ErrInvalidPatchHeader("UPS"));

            int pos = 4;
            long inputSize = ReadVli(pPat, ref pos, patch.Length);
            long outputSize = ReadVli(pPat, ref pos, patch.Length);

            if (input.LongLength != inputSize)
                throw new InvalidDataException(Loc.S.ErrSourceSizeMismatch(inputSize.ToString("N0"), input.LongLength.ToString("N0")));

            byte[] output = new byte[outputSize];

            Buffer.BlockCopy(input, 0, output, 0, (int)Math.Min(input.Length, outputSize));

            fixed (byte* pOut = output)
            {
                long outOffset = 0;
                int patchEnd = patch.Length - 12;

                while (pos < patchEnd)
                {
                    ct.ThrowIfCancellationRequested();

                    long skip = ReadVli(pPat, ref pos, patchEnd);

                    outOffset += skip;

                    while (pos < patchEnd)
                    {
                        ct.ThrowIfCancellationRequested();

                        byte b = pPat[pos++];

                        if (b == 0)
                            break;

                        if (outOffset < outputSize)
                            pOut[outOffset] ^= b;

                        outOffset++;
                    }

                    outOffset++;

                    if (progress != null && pos % Math.Max(1, patchEnd / 100) == 0)
                        progress.Report(new ProgressInfo { Percent = (int)(pos / (double)patchEnd * 100) });
                }
            }

            uint srcCrc = *(uint*)(pPat + patch.Length - 12);
            uint dstCrc = *(uint*)(pPat + patch.Length - 8);
            uint patCrc = *(uint*)(pPat + patch.Length - 4);

            if (CalculateCrc32(input) != srcCrc)
                throw new InvalidDataException(Loc.S.ErrInputCrc32Mismatch);

            if (CalculateCrc32(output) != dstCrc)
                throw new InvalidDataException(Loc.S.ErrOutputCrc32Mismatch);

            if (CalculateCrc32(patch, patch.Length - 4) != patCrc)
                throw new InvalidDataException(Loc.S.ErrPatchCrc32Mismatch);

            return output;
        }
    }

    private unsafe static long ReadVli(byte* patch, ref int pos, int maxLen)
    {
        long value = 0, shift = 1;

        while (pos < maxLen)
        {
            byte b = patch[pos++];

            value += (b & 0x7f) * shift;

            if ((b & 0x80) != 0)
                break;

            shift <<= 7;
            value += shift;
        }

        return value;
    }

    private static void WriteVli(Stream s, long value)
    {
        while (true)
        {
            byte b = (byte)(value & 0x7f);

            value >>= 7;

            if (value == 0)
            {
                s.WriteByte((byte)(b | 0x80));
                break;
            }

            s.WriteByte(b);

            value--;
        }
    }

    private unsafe static uint CalculateCrc32(byte[] data, int length = -1)
    {
        if (length == -1)
            length = data.Length;

        uint crc = 0xFFFFFFFF;

        fixed (byte* p = data)
            for (int i = 0; i < length; i++)
            {
                crc ^= p[i];

                for (int j = 0; j < 8; j++)
                    crc = (crc >> 1) ^ ((crc & 1) * 0xEDB88320);
            }

        return ~crc;
    }

    private static void ValidateInputFiles(params string[] paths)
    {
        foreach (var path in paths)
            if (!File.Exists(path))
                throw new FileNotFoundException(Loc.S.ErrFileNotFoundName(path));
    }
}