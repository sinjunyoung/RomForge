using RomForge.Core.Interfaces;
using RomForge.Core.ViewModels;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Models.PSP;

public class PbpFileItem(string filePath) : FileItemBase(filePath), Interfaces.IConvertible, IExtensionColored
{
    public Brush ExtensionBackground => ExtensionColorMap.Resolve(Extension, ColorMap);

    public static Brush ExtensionForeground => ExtensionForegroundBrush;

    private static readonly Brush ExtensionForegroundBrush = CreateForegroundBrush();

    private static readonly Dictionary<string, string> ColorMap = new()
    {
        ["pbp"] = "#D4A8FF",
    };

    private static SolidColorBrush CreateForegroundBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));

        brush.Freeze();

        return brush;
    }

    public BitmapSource? Icon { get; set => SetProperty(ref field, value); }

    public string? TitleId { get; set => SetProperty(ref field, value); } = string.Empty;

    public string? TitleName { get => string.IsNullOrEmpty(field) ? TitleId : field; set => SetProperty(ref field, value); } = string.Empty;

    public string? TitleLocalName { get; set => SetProperty(ref field, value); } = string.Empty;

    public List<string> Languages { get; set => SetProperty(ref field, value); } = [];

    protected override string FormatSize(long bytes) => PickPack.Disk.Helpers.FileSize.FormatSize(bytes);

    public List<string> AvailableFormats { get; } = ["CUE"];

    public string SelectedTargetFormat
    {
        get => "CUE"; set { }
    }
}