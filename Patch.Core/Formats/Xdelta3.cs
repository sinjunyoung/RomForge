using Common;
using Patch.Core.Formats.Xdelta.Models;
using Patch.Core.Formats.Xdelta.Services;

namespace Patch.Core.Formats;

public static class Xdelta3
{
    public static void ApplyPatch(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, patchPath);

        byte[] patchData = File.ReadAllBytes(patchPath);
        using var fileSource = new Xd3FileBlockSource(sourcePath);
        using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        var xd3Source = fileSource.CreateSource();

        try
        {
            if (progress is null)
            {
                Xd3Decoder.Decode(patchData, xd3Source, fileSource, outStream);
                return;
            }

            long total = fileSource.FileLength;
            var reporter = new ProgressReporter("패치중...", string.Empty, total, progress);
            var report = reporter.CreateAction();

            Xd3Decoder.Decode(patchData, xd3Source, fileSource, outStream);
        }
        catch (Xd3Exception ex)
        {
            throw Translate(ex);
        }
    }

    public static byte[] ApplyPatch(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var xd3Source = Xd3InMemorySource.CreateSource(sourceData);
        var blockSource = new Xd3InMemorySource(sourceData);
        using var outStream = new MemoryStream();

        try
        {
            if (progress is null)
            {
                Xd3Decoder.Decode(patchData, xd3Source, blockSource, outStream);

                return outStream.ToArray();
            }

            long total = sourceData.Length;
            var reporter = new ProgressReporter("패치중...", string.Empty, total, progress);
            var report = reporter.CreateAction();

            Xd3Decoder.Decode(patchData, xd3Source, blockSource, outStream);
        }
        catch (Xd3Exception ex)
        {
            throw Translate(ex);
        }

        return outStream.ToArray();
    }

    public static void CreatePatch(string sourcePath, string newPath, string patchPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) => throw new NotSupportedException("xdelta 패치 생성 기능은 현재 지원되지 않습니다.");

    private static InvalidOperationException Translate(Xd3Exception ex)
    {
        if (ex.Message.Contains("checksum mismatch", StringComparison.OrdinalIgnoreCase))
            return new InvalidOperationException("원본 파일이 패치 파일과 일치하지 않습니다. (체크섬 불일치)");

        if (ex.Message.Contains("not yet supported", StringComparison.OrdinalIgnoreCase))
            return new InvalidOperationException($"지원되지 않는 xdelta 패치 형식입니다: {ex.Message}");

        return new InvalidOperationException($"xdelta 패치 적용 실패: {ex.Message}");
    }

    private static void ValidateInputFiles(params string[] paths)
    {
        foreach (var path in paths)
            if (!File.Exists(path))
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {path}");
    }
}