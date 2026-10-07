using Common.WPF.ViewModels;
using System.IO;
using System.Windows.Media;
using WiiU.Core.Services;

namespace RomForge.Core.Models.WiiU;

public class WiiUFileItem(string filePath) : ConvertibleFileItemBase(filePath, "미지원")
{
    public string? TitleName { get; set; }

    public string TitleIdHex { get; set; } = "0000000000000000";

    public int TitleVersion { get; set; }

    public ImageSource? Icon { get; set; }

    public string ShortDescription => string.IsNullOrWhiteSpace(TitleName) ? FileName : TitleName!;

    public string TitleIdVersionDisplay => $"{TitleIdHex.ToUpperInvariant()} v{TitleVersion}";

    public Brush ExtensionBackground => ExtensionColorMap.Resolve(Extension, ColorMap);

    private static readonly Dictionary<string, string> ColorMap = new()
    {
        ["wup"] = "#94C8FF",
        ["loadiine"] = "#FFD494",
        ["wud"] = "#9E5B24",
        ["wux"] = "#9E5B24",
        ["wua"] = "#94FFB5",
    };

    public override string Extension
    {
        get
        {
            if (System.IO.Directory.Exists(FilePath))
            {
                if (WupTitleSource.LooksLikeWupFolder(FilePath))
                    return "wup";

                if (LooksLikeLoadiineFolder(FilePath))
                    return "loadiine";

                return string.Empty;
            }

            return base.Extension;
        }
    }

    private static bool LooksLikeLoadiineFolder(string path) => System.IO.Directory.Exists(Path.Combine(path, "code")) && System.IO.Directory.Exists(Path.Combine(path, "content")) && System.IO.Directory.Exists(Path.Combine(path, "meta"));

    protected override long CalculateSize(string filePath)
    {
        if (!System.IO.Directory.Exists(filePath))
            return base.CalculateSize(filePath);

        long size = 0;

        foreach (var file in new DirectoryInfo(filePath).EnumerateFiles("*", SearchOption.AllDirectories))
            size += file.Length;

        return size;
    }

    protected override IReadOnlyList<string> GetAvailableFormats(string extension) => extension switch
    {
        "wup" => ["Loadiine", "WUA"],
        "loadiine" => ["WUP", "WUA"],
        "wud" or "wux" => ["WUA", "WUP", "Loadiine"],
        "wua" => ["WUP", "Loadiine"],
        _ => [],
    };

    protected override string FormatSize(long bytes) => PickPack.Disk.ETC.FileSize.FormatSize(bytes);
}