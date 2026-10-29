using N3DS.Core.Enums;
using RomForge.Core.ViewModels;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Models.N3DS;

public class FileItem(string filePath) : ConvertibleFileItemBase(filePath, "미지원")
{
    private BitmapSource? _icon;

    public BitmapSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string? TitleId
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string? ShortDescription
    {
        get => string.IsNullOrEmpty(field) ? TitleId : field;
        set => SetProperty(ref field, value);
    }

    public string? Publisher
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string? ProductCode
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool Crypto
    {
        get;
        set => SetProperty(ref field, value);
    }

    public TitleType Type => (TitleType)(Convert.ToUInt64(TitleId, 16) >> 32);

    public string RegionSortKey
    {
        get
        {
            if (string.IsNullOrEmpty(ProductCode) || ProductCode.Length < 7) 
                return "Z";

            try
            {
                int dashIndex = ProductCode.IndexOf('-', 4);

                if (dashIndex < 0 || dashIndex + 4 >= ProductCode.Length) 
                    return "Z";

                char regionChar = ProductCode[dashIndex + 4];

                return regionChar.ToString();
            }
            catch
            {
                return "Z";
            }
        }
    }

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
        ["zcci"] = "#D48843",
    };

    protected override IReadOnlyList<string> GetAvailableFormats(string extension) => extension switch
    {
        "3ds" => ["CIA", "ZCCI"],
        "cci" => ["CIA", "ZCCI"],
        "cia" => ["CCI", "ZCCI"],
        "zcci" => ["CCI", "CIA"],
        _ => []
    };

    protected override string FormatSize(long bytes) => PickPack.Disk.Helpers.FileSize.FormatSize(bytes);
}