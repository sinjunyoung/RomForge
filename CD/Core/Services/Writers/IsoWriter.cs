using CD.Core.Models;
using Common;

namespace CD.Core.Services.Writers;

public static class IsoWriter
{
    public static bool CanWrite(DiscImage image) => CookedIsoWriter.CanWrite(image);

    public static Task<string> WriteAsync(DiscImage image, string outputDir, string outputBaseName, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) => CookedIsoWriter.WriteAsync(image, outputDir, outputBaseName, progress, ct);
}