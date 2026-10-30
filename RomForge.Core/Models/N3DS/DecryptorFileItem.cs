using N3DS.Core.Enums;
using RomForge.Core.ViewModels;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Models.N3DS;

public class DecryptorFileItem(string filePath) : FileItemBase(filePath)
{
    public BitmapSource? Icon { get; set => SetProperty(ref field, value); }

    public string? TitleId { get; set => SetProperty(ref field, value); }

    public string? ShortDescription { get => string.IsNullOrEmpty(field) ? TitleId : field; set => SetProperty(ref field, value); }

    public string? Publisher { get; set => SetProperty(ref field, value); }

    public string? ProductCode { get; set => SetProperty(ref field, value); }

    public bool Crypto { get; set => SetProperty(ref field, value); }

    public TitleType Type => (TitleType)(Convert.ToUInt64(TitleId, 16) >> 32);

    public string TypeLabel => Type switch
    {
        TitleType.Application => "본편",
        TitleType.SystemApplication => "시스템",
        TitleType.Patch => "업데이트",
        TitleType.DlcContent => "DLC",
        _ => "기타",
    };

    public SolidColorBrush TypeBadgeColor => Type switch
    {
        TitleType.Application => new SolidColorBrush(Color.FromRgb(0x4F, 0x8E, 0xF7)),
        TitleType.SystemApplication => new SolidColorBrush(Color.FromRgb(0xF7, 0x9A, 0x3D)),
        TitleType.Patch => new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C)),
        TitleType.DlcContent => new SolidColorBrush(Color.FromRgb(0xC9, 0x7B, 0xF7)),
        _ => new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x6A)),
    };

    public Brush ExtensionBackground => ExtensionColorMap.Resolve(Extension, ColorMap);

    private static readonly Dictionary<string, string> ColorMap = new()
    {
        ["3ds"] = "#FFE094",
        ["cci"] = "#FFCE73",
        ["cia"] = "#C96F2C",
    };

    protected override string FormatSize(long bytes) => PickPack.Disk.Helpers.FileSize.FormatSize(bytes);
}