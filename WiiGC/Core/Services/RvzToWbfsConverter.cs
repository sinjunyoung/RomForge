using WiiGC.Core.Enums;
using Common.Localization;
using WiiGC.Core.Services.Wii;

namespace WiiGC.Core.Services;

public static class RvzToWbfsConverter
{
    public static void Convert(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        using (var reader = new RvzDiscReader(inputPath))
        {
            if (DiscHeader.FromRvzType(reader.DiscType) != DiscPlatform.Wii)
                throw new InvalidDataException(Loc.S.ErrRvzNotWiiForWbfs);
        }

        IsoToWbfsConverter.Convert(inputPath, outputPath, progress, ct);
    }
}