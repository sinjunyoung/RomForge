using Common.WPF.ViewModels;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Models.PS;

public class PbpFileItem(string filePath) : FileItemBase(filePath), Common.WPF.ViewModels.IConvertible, IExtensionColored
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

    private BitmapSource? _icon;
    private string _titleId = string.Empty;
    private string _titleName = string.Empty;
    private string _titleLocalName = string.Empty;
    private List<string> _languages = [];

    public BitmapSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string TitleId
    {
        get => _titleId;
        set => SetProperty(ref _titleId, value);
    }

    public string TitleName
    {
        get => string.IsNullOrEmpty(_titleName) ? TitleId : _titleName;
        set => SetProperty(ref _titleName, value);
    }

    public string TitleLocalName
    {
        get => _titleLocalName;
        set => SetProperty(ref _titleLocalName, value);
    }

    public List<string> Languages
    {
        get => _languages;
        set => SetProperty(ref _languages, value);
    }

    protected override string FormatSize(long bytes) => PickPack.Disk.ETC.FileSize.FormatSize(bytes);

    public List<string> AvailableFormats { get; } = ["CUE"];

    public string SelectedTargetFormat
    {
        get => "CUE";
        set { }
    }
}