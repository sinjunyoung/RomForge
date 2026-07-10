using Common;

namespace Patch.Core.Formats;

public static class Bps
{
    private static readonly byte[] HeaderBytes = [(byte)'B', (byte)'P', (byte)'S', (byte)'1'];

    public static async Task ApplyPatchAsync(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, patchPath);

        byte[] source = await File.ReadAllBytesAsync(sourcePath, ct);
        byte[] patch = await File.ReadAllBytesAsync(patchPath, ct);
        byte[] result = await Task.Run(() => Decode(source, patch, progress, ct), ct);

        await File.WriteAllBytesAsync(outputPath, result, ct);
    }

    public static Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => Task.Run(() => Decode(sourceData, patchData, progress, ct), ct);

    public static async Task CreatePatchAsync(string sourcePath, string newPath, string patchPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, newPath);

        byte[] source = await File.ReadAllBytesAsync(sourcePath, ct);
        byte[] target = await File.ReadAllBytesAsync(newPath, ct);
        byte[] result = await Task.Run(() => Encode(source, target, progress, ct), ct);

        await File.WriteAllBytesAsync(patchPath, result, ct);
    }

    private unsafe static byte[] Decode(byte[] source, byte[] patch, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (patch.Length < 12)
            throw new InvalidDataException("BPS 패치 파일이 너무 짧습니다.");

        fixed (byte* pSrc = source, pPat = patch)
        {
            if (pPat[0] != 'B' || pPat[1] != 'P' || pPat[2] != 'S' || pPat[3] != '1')
                throw new InvalidDataException("유효하지 않은 BPS 헤더입니다.");

            int pos = 4;
            long sourceSize = ReadVli(pPat, ref pos, patch.Length);
            long targetSize = ReadVli(pPat, ref pos, patch.Length);
            long metadataSize = ReadVli(pPat, ref pos, patch.Length);

            if (source.LongLength != sourceSize)
                throw new InvalidDataException($"원본 파일 크기와 패치 파일 크기가 일치하지 않습니다. (패치가 기대하는 크기: {sourceSize:N0} bytes, 실제 파일 크기: {source.LongLength:N0} bytes)");

            if (metadataSize < 0 || pos + metadataSize > patch.Length - 12)
                throw new InvalidDataException("BPS 메타데이터 크기가 유효하지 않습니다.");

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
                        throw new InvalidDataException("패치 명령의 길이가 대상 파일 범위를 벗어납니다. 패치 파일이 손상되었거나 원본 파일이 일치하지 않습니다.");

                    switch (command)
                    {
                        case 0:
                            if (outOffset + (long)length > sourceSize)
                                throw new InvalidDataException("SourceRead 명령이 원본 파일 범위를 벗어납니다. 원본 파일을 확인하세요.");

                            Buffer.MemoryCopy(pSrc + outOffset, pTar + outOffset, targetSize - outOffset, length);

                            outOffset += length;

                            break;

                        case 1:
                            if (pos + (long)length > patchEnd)
                                throw new InvalidDataException("TargetRead 명령이 패치 파일 범위를 벗어납니다. 패치 파일이 손상되었을 수 있습니다.");

                            Buffer.MemoryCopy(pPat + pos, pTar + outOffset, targetSize - outOffset, length);

                            pos += length;
                            outOffset += length;

                            break;

                        case 2:
                            long od2 = ReadVli(pPat, ref pos, patchEnd);

                            srcRelOffset += (od2 & 1) == 1 ? -(int)(od2 >> 1) : (int)(od2 >> 1);

                            if (srcRelOffset < 0 || srcRelOffset + (long)length > sourceSize)
                                throw new InvalidDataException("SourceCopy 명령이 원본 파일 범위를 벗어납니다. 원본 파일을 확인하세요.");

                            Buffer.MemoryCopy(pSrc + srcRelOffset, pTar + outOffset, targetSize - outOffset, length);

                            outOffset += length;
                            srcRelOffset += length;

                            break;

                        case 3:
                            long od3 = ReadVli(pPat, ref pos, patchEnd);

                            tarRelOffset += (od3 & 1) == 1 ? -(int)(od3 >> 1) : (int)(od3 >> 1);

                            if (tarRelOffset < 0 || tarRelOffset >= outOffset)
                                throw new InvalidDataException("TargetCopy 명령의 참조 위치가 유효하지 않습니다. 패치 파일이 손상되었을 수 있습니다.");

                            for (int i = 0; i < length; i++)
                                pTar[outOffset + i] = pTar[tarRelOffset + i];

                            outOffset += length;
                            tarRelOffset += length;

                            break;
                    }

                    if (progress != null && pos % Math.Max(1, patchEnd / 100) == 0)
                        progress.Report(new ProgressInfo { Percent = pos / patchEnd });
                }

                if (outOffset != targetSize)
                    throw new InvalidDataException("패치 적용 결과 크기가 예상 대상 파일 크기와 일치하지 않습니다. 패치가 손상되었을 수 있습니다.");
            }

            return target;
        }
    }

    private unsafe static byte[] Encode(byte[] source, byte[] target, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        using var ms = new MemoryStream();

        ms.Write(HeaderBytes, 0, 4);
        WriteVli(ms, source.Length);
        WriteVli(ms, target.Length);
        WriteVli(ms, 0);

        fixed (byte* pSrc = source, pTar = target)
        {
            int outOffset = 0;

            while (outOffset < target.Length)
            {
                ct.ThrowIfCancellationRequested();

                if (outOffset < source.Length && pSrc[outOffset] == pTar[outOffset])
                {
                    int len = 0;

                    while (outOffset + len < source.Length && outOffset + len < target.Length && pSrc[outOffset + len] == pTar[outOffset + len])
                        len++;

                    WriteVli(ms, (long)((len - 1) << 2) | 0);

                    outOffset += len;
                }
                else
                {
                    int runLen = 0;

                    while (outOffset + runLen < target.Length && runLen < 0xFFFF)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (outOffset + runLen < source.Length && pSrc[outOffset + runLen] == pTar[outOffset + runLen])
                            break;

                        runLen++;
                    }

                    if (runLen == 0) runLen = 1;

                    WriteVli(ms, (long)((runLen - 1) << 2) | 1);
                    ms.Write(target, outOffset, runLen);

                    outOffset += runLen;
                }

                if (progress != null && outOffset % 1000 == 0)
                    progress.Report(new ProgressInfo { Percent = outOffset / target.Length });
            }
        }

        byte[] raw = ms.ToArray();
        uint crc = CalculateCrc32(raw);
        byte[] finalPatch = new byte[raw.Length + 4];

        Buffer.BlockCopy(raw, 0, finalPatch, 0, raw.Length);
        BitConverter.GetBytes(crc).CopyTo(finalPatch, finalPatch.Length - 4);

        return finalPatch;
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

    private static void WriteVli(Stream s, long value)
    {
        while (true)
        {
            byte b = (byte)(value & 0x7F);

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
        if (length == -1) length = data.Length;

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