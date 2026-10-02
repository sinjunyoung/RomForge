using DolphinTool.Core.Services.Wii;

namespace DolphinTool.Core.Services;

public static class RvzToWbfsConverter
{
    public static void Convert(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        string tempPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!, $"{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var reader = new RvzDiscReader(inputPath))
            {
                if (reader.DiscType != 2)
                    throw new InvalidDataException("Wii RVZ 파일이 아닙니다. WBFS는 Wii 디스크만 지원합니다.");
            }

            RvzToIsoConverter.Convert(inputPath, tempPath, p => progress?.Invoke(p * 0.5), ct);
            IsoToWbfsConverter.Convert(tempPath, outputPath, p => progress?.Invoke(0.5 + p * 0.5), ct);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch { }
        }
    }
}