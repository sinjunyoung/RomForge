using Common.WPF.ViewModels;
using RomForge.Core.Models.Compression;
using RomForge.Core.Services.Compression;
using System.Windows.Media;

namespace RomForge.Core.Models.CD;

public class DiscConvertFileItem : ConvertibleFileItemBase
{
    public RomFormat DetectedFormat { get; }

    public DiscConvertFileItem(string filePath) : base(filePath)
    {
        DetectedFormat = GetDetectedFormat(filePath, Extension);
    }

    public Brush ExtensionBackground => ExtensionColorMap.Resolve(Extension, ColorMap);

    private static readonly Dictionary<string, string> ColorMap = new()
    {
        ["chd"] = "#A2C4FC",
        ["iso"] = "#FFF9A6",
        ["cue"] = "#EAE2A6",
        ["gdi"] = "#D2DAA5",
        ["cso"] = "#94FFB5",
        ["zso"] = "#FFD494",
        ["gcm"] = "#C9BFFF",
        ["gcz"] = "#9485EA",
        ["wbfs"] = "#B6D0FF",
        ["wia"] = "#7A9CE6",
        ["rvz"] = "#E2CEFF",
    };

    private static RomFormat GetDetectedFormat(string filePath, string extension) => extension switch
    {
        "cso" => RomFormat.Cso,
        "zso" => RomFormat.Zso,
        _ => FormatDetector.Detect(filePath).Format
    };

    protected override IReadOnlyList<string> GetAvailableFormats(string extension)
    {
        if (extension == "cso")
            return ["ISO", "ZSO", "CHD"];

        if (extension == "zso")
            return ["ISO", "CSO", "CHD"];

        var detected = FormatDetector.Detect(FilePath);

        if (detected.Format == RomFormat.Unknown || string.IsNullOrEmpty(detected.OutputExtension))
            return [];

        var defaultTarget = detected.OutputExtension.ToUpperInvariant() switch
        {
            "CUE" => "CUE",
            var ext => ext
        };

        IEnumerable<string> formats;

        if (extension.Equals("chd", StringComparison.OrdinalIgnoreCase) || detected.Format == RomFormat.Chd)
        {
            if (detected.OutputExtension.Equals("iso", StringComparison.OrdinalIgnoreCase) || detected.OutputExtension.Equals("cue", StringComparison.OrdinalIgnoreCase))
                formats = [defaultTarget, "CSO", "ZSO", "CHD"];
            else
                formats = [defaultTarget, "CHD"];
        }
        else if (detected.Format == RomFormat.Iso)
        {
            formats = [defaultTarget, "CSO", "ZSO", "CHD"];
        }
        else
        {
            formats = [defaultTarget];
        }

        return [.. formats.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    protected override string FormatSize(long bytes) => PickPack.Disk.ETC.FileSize.FormatSize(bytes);
}