using Common;

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

    public static Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => Task.Run(() => Decode(sourceData, patchData, progress, ct), ct);

    public static async Task CreatePatchAsync(string sourcePath, string newPath, string patchPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, newPath);

        byte[] source = await File.ReadAllBytesAsync(sourcePath, ct);
        byte[] target = await File.ReadAllBytesAsync(newPath, ct);
        byte[] patch = await Task.Run(() => Encode(source, target, progress, ct), ct);

        await File.WriteAllBytesAsync(patchPath, patch, ct);
    }

    private unsafe static byte[] Decode(byte[] input, byte[] patch, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (patch.Length < 12) throw new InvalidDataException("UPS 패치 파일이 너무 짧습니다.");

        fixed (byte* pPat = patch)
        {
            if (pPat[0] != 'U' || pPat[1] != 'P' || pPat[2] != 'S' || pPat[3] != '1')
                throw new InvalidDataException("유효하지 않은 UPS 헤더입니다.");

            int pos = 4;
            long inputSize = ReadVli(pPat, ref pos, patch.Length);
            long outputSize = ReadVli(pPat, ref pos, patch.Length);

            if (input.LongLength != inputSize)
                throw new InvalidDataException($"원본 파일 크기와 패치 파일 크기가 일치하지 않습니다. (패치가 기대하는 크기: {inputSize:N0} bytes, 실제 파일 크기: {input.LongLength:N0} bytes) ");

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

                    if (progress != null && pos % Math.Max(1, patchEnd / 100) == 0)
                        progress.Report(new ProgressInfo { Percent = pos / patchEnd });
                }
            }

            uint srcCrc = *(uint*)(pPat + patch.Length - 12);
            uint dstCrc = *(uint*)(pPat + patch.Length - 8);
            uint patCrc = *(uint*)(pPat + patch.Length - 4);

            if (CalculateCrc32(input) != srcCrc)
                throw new InvalidDataException("Input CRC32 불일치");

            if (CalculateCrc32(output) != dstCrc)
                throw new InvalidDataException("Output CRC32 불일치");

            if (CalculateCrc32(patch, patch.Length - 4) != patCrc)
                throw new InvalidDataException("Patch CRC32 불일치");

            return output;
        }
    }

    private unsafe static byte[] Encode(byte[] source, byte[] target, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        using var ms = new MemoryStream();

        ms.Write(HeaderBytes, 0, 4);
        WriteVli(ms, source.Length);
        WriteVli(ms, target.Length);

        int maxLen = Math.Max(source.Length, target.Length);

        fixed (byte* pSrc = source, pTar = target)
        {
            int i = 0;
            int lastOffset = 0;

            while (i < maxLen)
            {
                ct.ThrowIfCancellationRequested();

                byte s = i < source.Length ? pSrc[i] : (byte)0;
                byte t = i < target.Length ? pTar[i] : (byte)0;

                if (s != t)
                {
                    WriteVli(ms, i - lastOffset);

                    int blockStart = i;

                    while (i < maxLen)
                    {
                        ct.ThrowIfCancellationRequested();

                        if ((i < source.Length ? pSrc[i] : (byte)0) == (i < target.Length ? pTar[i] : (byte)0))
                            break;

                        i++;
                    }

                    byte[] xorBlock = new byte[i - blockStart + 1];

                    for (int j = 0; j < i - blockStart; j++)
                    {
                        byte sb = (blockStart + j) < source.Length ? pSrc[blockStart + j] : (byte)0;
                        byte tb = (blockStart + j) < target.Length ? pTar[blockStart + j] : (byte)0;
                        xorBlock[j] = (byte)(sb ^ tb);
                    }

                    xorBlock[i - blockStart] = 0;
                    ms.Write(xorBlock, 0, xorBlock.Length);

                    lastOffset = i;
                }
                else i++;

                if (progress != null && i % 1000 == 0)
                    progress.Report(new ProgressInfo { Percent = i / maxLen });
            }
        }

        byte[] raw = ms.ToArray();
        byte[] final = new byte[raw.Length + 12];

        Buffer.BlockCopy(raw, 0, final, 0, raw.Length);
        BitConverter.GetBytes(CalculateCrc32(source)).CopyTo(final, final.Length - 12);
        BitConverter.GetBytes(CalculateCrc32(target)).CopyTo(final, final.Length - 8);
        BitConverter.GetBytes(CalculateCrc32(raw)).CopyTo(final, final.Length - 4);

        return final;
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
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {path}");
    }
}