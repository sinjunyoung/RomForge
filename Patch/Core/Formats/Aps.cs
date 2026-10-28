using Common;

namespace Patch.Core.Formats;

public static class Aps
{
    private static readonly byte[] HeaderBytes = [(byte)'A', (byte)'P', (byte)'S', (byte)'1'];

    public static async Task ApplyPatchAsync(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, patchPath);

        byte[] source = await File.ReadAllBytesAsync(sourcePath, ct);
        byte[] patch = await File.ReadAllBytesAsync(patchPath, ct);
        byte[] result = await Task.Run(() => Decode(source, patch, progress, ct), ct);

        await File.WriteAllBytesAsync(outputPath, result, ct);
    }

    public static async Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) => await Task.Run(() => Decode(sourceData, patchData, progress, ct), ct);

    private unsafe static byte[] Decode(byte[] source, byte[] patch, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (patch.Length < 8) throw new InvalidDataException("APS 패치 파일이 너무 짧습니다.");

        fixed (byte* pPat = patch)
        {
            if (pPat[0] != 'A' || pPat[1] != 'P' || pPat[2] != 'S' || pPat[3] != '1')
                throw new InvalidDataException("유효하지 않은 APS 헤더입니다.");

            byte[] output = new byte[source.Length];
            Buffer.BlockCopy(source, 0, output, 0, source.Length);

            int pos = 4;

            fixed (byte* pOut = output)
            {
                while (pos + 5 <= patch.Length)
                {
                    ct.ThrowIfCancellationRequested();

                    uint offset = *(uint*)(pPat + pos);

                    pos += 4;

                    byte length = pPat[pos++];

                    if (length == 0 || pos + length > patch.Length)
                        break;

                    long offsetLong = offset;
                    long lengthLong = length;

                    if (offsetLong + lengthLong > output.Length)
                        throw new InvalidDataException($"패치 레코드가 원본 파일 범위를 벗어납니다. (오프셋: {offsetLong:N0}, 길이: {lengthLong}, 원본 파일 크기: {output.Length:N0} bytes) ");

                    Buffer.MemoryCopy(pPat + pos, pOut + offsetLong, output.Length - offsetLong, length);

                    pos += length;

                    if (progress != null && pos % Math.Max(1, patch.Length / 100) == 0)
                        progress.Report(new ProgressInfo((int)((double)pos / patch.Length * 100), string.Empty, string.Empty, string.Empty, string.Empty));
                }
            }

            return output;
        }
    }

    private static void ValidateInputFiles(params string[] paths)
    {
        foreach (var path in paths)
            if (!File.Exists(path))
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {path}");
    }
}