using WiiGC.Core.Models;
using WiiGC.Core.Services.Wii;

namespace WiiGC.Core.Services;

public static class RvzToWbfsConverter
{
    public static void Convert(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        using (var reader = new RvzDiscReader(inputPath))
        {
            if (DiscHeader.FromRvzType(reader.DiscType) != DiscPlatform.Wii)
                throw new InvalidDataException("Wii RVZ 파일이 아닙니다. WBFS는 Wii 디스크만 지원합니다.");
        }

        IsoToWbfsConverter.Convert(inputPath, outputPath, progress, ct);
    }
}