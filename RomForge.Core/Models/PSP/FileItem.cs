using RomForge.Core.ViewModels;
using System.Windows.Media;

namespace RomForge.Core.Models.PSP;

public class FileItem(string filePath) : ConvertibleFileItemBase(filePath, "미지원")
{
    public Brush ExtensionBackground => ExtensionColorMap.Resolve(Extension, ColorMap);

    private static readonly Dictionary<string, string> ColorMap = new()
    {
        ["iso"] = "#94C8FF",
        ["cso"] = "#94FFB5",
        ["zso"] = "#FFD494",
        ["chd"] = "#D494FF",
    };

    protected override IReadOnlyList<string> GetAvailableFormats(string extension) => extension switch
    {
        "iso" => ["CSO", "ZSO", "CHD"],
        "cso" => ["ISO", "ZSO", "CHD"],
        "zso" => ["ISO", "CSO", "CHD"],
        "chd" => ["ISO", "CSO", "ZSO"],
        _ => []
    };

    protected override string FormatSize(long bytes) => PickPack.Disk.Helpers.FileSize.FormatSize(bytes);
}