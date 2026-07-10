using Common;

namespace Patch.Core.Formats;

public static class Ips
{
    private static readonly byte[] PatchHeader = [(byte)'P', (byte)'A', (byte)'T', (byte)'C', (byte)'H'];
    private static readonly byte[] PatchFooter = [(byte)'E', (byte)'O', (byte)'F'];

    public static async Task CreatePatchAsync(string sourcePath, string newPath, string patchPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, newPath);

        byte[] original = await File.ReadAllBytesAsync(sourcePath, ct);
        byte[] modified = await File.ReadAllBytesAsync(newPath, ct);
        byte[] patch = await Task.Run(() => Encode(original, modified, progress, ct), ct);

        await File.WriteAllBytesAsync(patchPath, patch, ct);
    }

    public static async Task ApplyPatchAsync(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, patchPath);

        byte[] rom = await File.ReadAllBytesAsync(sourcePath, ct);
        byte[] ips = await File.ReadAllBytesAsync(patchPath, ct);
        byte[] result = await Task.Run(() => Decode(rom, ips, progress, ct), ct);

        await File.WriteAllBytesAsync(outputPath, result, ct);
    }

    public static Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => Task.Run(() => Decode(sourceData, patchData, progress, ct), ct);

    private unsafe static byte[] Encode(byte[] original, byte[] modified, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        ms.Write(PatchHeader, 0, PatchHeader.Length);

        int maxLen = Math.Max(original.Length, modified.Length);

        fixed (byte* pOrg = original, pMod = modified)
        {
            int pos = 0;

            while (pos < maxLen)
            {
                ct.ThrowIfCancellationRequested();

                byte orgByte = pos < original.Length ? pOrg[pos] : (byte)0;
                byte modByte = pos < modified.Length ? pMod[pos] : (byte)0;

                if (orgByte != modByte)
                {
                    int start = pos;

                    while (pos < maxLen && (pos - start) < 0xFFFF)
                    {
                        ct.ThrowIfCancellationRequested();

                        if ((pos < original.Length ? pOrg[pos] : (byte)0) == (pos < modified.Length ? pMod[pos] : (byte)0))
                            break;

                        pos++;
                    }

                    int size = pos - start;
                    byte firstMod = start < modified.Length ? pMod[start] : (byte)0;
                    bool isRle = size >= 3;

                    if (isRle)
                    {
                        for (int j = start + 1; j < start + size; j++)
                        {
                            ct.ThrowIfCancellationRequested();

                            if ((j < modified.Length ? pMod[j] : (byte)0) != firstMod)
                            {
                                isRle = false;
                                break;
                            }
                        }
                    }

                    ms.WriteByte((byte)((start >> 16) & 0xFF));
                    ms.WriteByte((byte)((start >> 8) & 0xFF));
                    ms.WriteByte((byte)(start & 0xFF));

                    if (isRle)
                    {
                        ms.WriteByte(0);
                        ms.WriteByte(0);
                        ms.WriteByte((byte)((size >> 8) & 0xFF));
                        ms.WriteByte((byte)(size & 0xFF));
                        ms.WriteByte(firstMod);
                    }
                    else
                    {
                        ms.WriteByte((byte)((size >> 8) & 0xFF));
                        ms.WriteByte((byte)(size & 0xFF));

                        int available = Math.Min(size, modified.Length - start);

                        ms.Write(modified, start, Math.Max(0, available));

                        for (int i = available; i < size; i++)
                            ms.WriteByte(0);
                    }
                }
                else pos++;

                if (progress != null && pos % Math.Max(1, maxLen / 100) == 0)
                {
                    progress.Report(new ProgressInfo
                    {
                        Percent = pos / maxLen
                    });
                }
            }
        }

        ms.Write(PatchFooter, 0, PatchFooter.Length);

        return ms.ToArray();
    }

    private unsafe static byte[] Decode(byte[] rom, byte[] ips, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (ips.Length < 8)
            throw new InvalidDataException("IPS 파일이 너무 짧습니다.");

        fixed (byte* pIps = ips)
        {
            if (pIps[0] != 'P' || pIps[1] != 'A' || pIps[2] != 'T' || pIps[3] != 'C' || pIps[4] != 'H')
                throw new InvalidDataException("유효하지 않은 IPS 헤더입니다.");

            byte[] result = new byte[rom.Length];

            Buffer.BlockCopy(rom, 0, result, 0, rom.Length);

            int actualFinalSize = rom.Length;
            int pos = 5;

            while (pos + 3 <= ips.Length)
            {
                ct.ThrowIfCancellationRequested();

                if (pIps[pos] == 'E' && pIps[pos + 1] == 'O' && pIps[pos + 2] == 'F')
                    break;

                int offset = (pIps[pos] << 16) | (pIps[pos + 1] << 8) | pIps[pos + 2];

                pos += 3;

                if (pos + 2 > ips.Length)
                    break;

                int size = (pIps[pos] << 8) | pIps[pos + 1];

                pos += 2;

                if (size == 0)
                {
                    if (pos + 3 > ips.Length)
                        throw new InvalidDataException("IPS 패치 파일이 손상되었습니다. (RLE 레코드가 파일 끝에서 잘려 있습니다.)");

                    int rleCount = (pIps[pos] << 8) | pIps[pos + 1];
                    byte rleValue = pIps[pos + 2];

                    pos += 3;

                    EnsureCapacity(ref result, offset + rleCount);

                    if (offset + rleCount > actualFinalSize)
                        actualFinalSize = offset + rleCount;

                    fixed (byte* pRes = result)
                        for (int i = 0; i < rleCount; i++)
                            pRes[offset + i] = rleValue;
                }
                else
                {
                    if (pos + size > ips.Length)
                        throw new InvalidDataException("IPS 패치 파일이 손상되었습니다. (데이터 블록이 파일 끝에서 잘려 있습니다.)");

                    EnsureCapacity(ref result, offset + size);

                    if (offset + size > actualFinalSize)
                        actualFinalSize = offset + size;

                    fixed (byte* pRes = result)
                        Buffer.MemoryCopy(pIps + pos, pRes + offset, result.Length - offset, size);

                    pos += size;
                }

                progress?.Report(new ProgressInfo
                {
                    Percent = pos / ips.Length
                });
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

    private static void ValidateInputFiles(params string[] paths)
    {
        foreach (var path in paths)
            if (!File.Exists(path))
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {path}");
    }
}