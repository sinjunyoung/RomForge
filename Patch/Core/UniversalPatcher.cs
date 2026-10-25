using Common;
using Patch.Core.Enums;
using Patch.Core.Formats;

namespace Patch.Core;

public static class UniversalPatcher
{
    public static async Task ApplyPatchAsync(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"원본 파일을 찾을 수 없습니다: {sourcePath}");

        if (!File.Exists(patchPath))
            throw new FileNotFoundException($"패치 파일을 찾을 수 없습니다: {patchPath}");

        ct.ThrowIfCancellationRequested();

        PatchFormat format = await DetectFormatAsync(patchPath, ct);

        switch (format)
        {
            case PatchFormat.Xdelta: await Task.Run(() => Xdelta3.ApplyPatch(sourcePath, patchPath, outputPath, progress, ct), ct); break;
            case PatchFormat.Ips: await Ips.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
            case PatchFormat.Ips32: await Ips32.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
            case PatchFormat.Bps: await Bps.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
            case PatchFormat.Ups: await Ups.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
            case PatchFormat.Ppf: await Ppf.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
            case PatchFormat.Aps: await Aps.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
            default: throw new NotSupportedException("지원되지 않거나 유효하지 않은 패치 포맷입니다.");
        }
    }

    public static async Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        PatchFormat format = DetectFormat(patchData);

        return format switch
        {
            PatchFormat.Xdelta => await Task.Run(() => Xdelta3.ApplyPatch(sourceData, patchData, progress, ct), ct),
            PatchFormat.Ips => await Ips.ApplyPatchAsync(sourceData, patchData, progress, ct),
            PatchFormat.Ips32 => await Ips32.ApplyPatchAsync(sourceData, patchData, progress, ct),
            PatchFormat.Bps => await Bps.ApplyPatchAsync(sourceData, patchData, progress, ct),
            PatchFormat.Ups => await Ups.ApplyPatchAsync(sourceData, patchData, progress, ct),
            PatchFormat.Ppf => await Ppf.ApplyPatchAsync(sourceData, patchData, progress, ct),
            PatchFormat.Aps => await Aps.ApplyPatchAsync(sourceData, patchData, progress, ct),
            _ => throw new NotSupportedException("지원되지 않거나 유효하지 않은 패치 포맷입니다.")
        };
    }

    public static async Task<PatchFormat> DetectFormatAsync(string patchPath, CancellationToken ct = default)
    {
        if (!File.Exists(patchPath))
            throw new FileNotFoundException($"파일을 찾을 수 없습니다: {patchPath}");

        byte[] header = new byte[8];
        using var fs = new FileStream(patchPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        int read = await fs.ReadAsync(header, ct);

        return DetectFormat(header.AsSpan(0, read));
    }

    public static PatchFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return PatchFormat.Unknown;

        if (data.Length >= 3 && data[0] == 0xD6 && data[1] == 0xC3 && data[2] == 0xC4)
            return PatchFormat.Xdelta;

        if (data.Length >= 5 && data[0] == 'I' && data[1] == 'P' && data[2] == 'S' && data[3] == '3' && data[4] == '2')
            return PatchFormat.Ips32;

        if (data.Length >= 5 && data[0] == 'P' && data[1] == 'A' && data[2] == 'T' && data[3] == 'C' && data[4] == 'H')
            return PatchFormat.Ips;

        if (data[0] == 'B' && data[1] == 'P' && data[2] == 'S' && data[3] == '1')
            return PatchFormat.Bps;

        if (data[0] == 'U' && data[1] == 'P' && data[2] == 'S' && data[3] == '1')
            return PatchFormat.Ups;

        if (data.Length >= 3 && data[0] == 'P' && data[1] == 'P' && data[2] == 'F')
            return PatchFormat.Ppf;

        if (data[0] == 'A' && data[1] == 'P' && data[2] == 'S' && data[3] == '1')
            return PatchFormat.Aps;

        return PatchFormat.Unknown;
    }
}