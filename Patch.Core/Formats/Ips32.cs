using Common;

namespace Patch.Core.Formats;

public static class Ips32
{
    public static async Task ApplyPatchAsync(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"파일을 찾을 수 없습니다: {sourcePath}");

        if (!File.Exists(patchPath))
            throw new FileNotFoundException($"파일을 찾을 수 없습니다: {patchPath}");

        byte[] rom = await File.ReadAllBytesAsync(sourcePath, ct);
        byte[] ips = await File.ReadAllBytesAsync(patchPath, ct);
        byte[] result = await Task.Run(() => Decode(rom, ips, progress, ct), ct);

        await File.WriteAllBytesAsync(outputPath, result, ct);
    }

    public static Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => Task.Run(() => Decode(sourceData, patchData, progress, ct), ct);

    private unsafe static byte[] Decode(byte[] rom, byte[] ips, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (ips.Length < 5)
            throw new InvalidDataException("IPS32 파일이 너무 짧습니다.");

        fixed (byte* pIps = ips)
        {
            if (pIps[0] != 'I' || pIps[1] != 'P' || pIps[2] != 'S' || pIps[3] != '3' || pIps[4] != '2')
                throw new InvalidDataException("유효하지 않은 IPS32 헤더입니다.");

            byte[] result = new byte[rom.Length];

            Buffer.BlockCopy(rom, 0, result, 0, rom.Length);

            int actualFinalSize = rom.Length;
            int pos = 5;

            while (pos + 4 <= ips.Length)
            {
                ct.ThrowIfCancellationRequested();

                if (pIps[pos] == 'E' && pIps[pos + 1] == 'E' && pIps[pos + 2] == 'O' && pIps[pos + 3] == 'F')
                    break;

                long offset = ((long)pIps[pos] << 24) | ((long)pIps[pos + 1] << 16) | ((long)pIps[pos + 2] << 8) | pIps[pos + 3];

                pos += 4;

                if (pos + 2 > ips.Length)
                    break;

                int size = (pIps[pos] << 8) | pIps[pos + 1];

                pos += 2;

                if (size == 0)
                {
                    if (pos + 3 > ips.Length)
                        throw new InvalidDataException("IPS32 패치 파일이 손상되었습니다. (RLE 레코드가 파일 끝에서 잘려 있습니다.)");

                    int rleCount = (pIps[pos] << 8) | pIps[pos + 1];
                    byte rleValue = pIps[pos + 2];

                    pos += 3;

                    EnsureCapacity(ref result, (int)(offset + rleCount));

                    if (offset + rleCount > actualFinalSize)
                        actualFinalSize = (int)(offset + rleCount);

                    fixed (byte* pRes = result)
                        for (int i = 0; i < rleCount; i++)
                            pRes[offset + i] = rleValue;
                }
                else
                {
                    if (pos + size > ips.Length)
                        throw new InvalidDataException("IPS32 패치 파일이 손상되었습니다. (데이터 블록이 파일 끝에서 잘려 있습니다.)");

                    EnsureCapacity(ref result, (int)(offset + size));

                    if (offset + size > actualFinalSize)
                        actualFinalSize = (int)(offset + size);

                    fixed (byte* pRes = result)
                        Buffer.MemoryCopy(pIps + pos, pRes + offset, result.Length - offset, size);

                    pos += size;
                }

                progress?.Report(new ProgressInfo { Percent = (int)(pos / (double)ips.Length * 100) });
            }

            if (result.Length != actualFinalSize)
                Array.Resize(ref result, actualFinalSize);

            return result;
        }
    }

    private static void EnsureCapacity(ref byte[] array, int requiredSize)
    {
        if (array.Length < requiredSize)
            Array.Resize(ref array, Math.Max(array.Length * 2, requiredSize));
    }
}