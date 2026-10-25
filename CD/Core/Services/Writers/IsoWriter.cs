using CD.Core.Models;
using Common;

namespace CD.Core.Services.Writers;

public static class IsoWriter
{
    public static async Task<string> WriteAsync(DiscImage image, string outputDir, string outputBaseName, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (image.Tracks.Count != 1)
            throw new NotSupportedException($"ISO 변환은 단일 트랙 이미지에서만 가능합니다(현재 트랙 수: {image.Tracks.Count}).");

        return await CookedIsoWriter.WriteAsync(image, outputDir, outputBaseName, progress, ct);
    }
}