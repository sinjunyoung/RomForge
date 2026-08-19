using _3DS.Core.Enums;
using Common.WPF.ViewModels;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Models._3DS;

public class _3DSFileItem(string filePath) : ConvertibleFileItemBase(filePath, "미지원")
{
    private BitmapSource? _icon;
    private string _titleId = string.Empty;
    private string _shortDescription = string.Empty;
    private string _publisher = string.Empty;
    private string _productCode = string.Empty;
    private bool _crypto;

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

    public string ShortDescription
    {
        get => string.IsNullOrEmpty(_shortDescription) ? TitleId : _shortDescription;
        set => SetProperty(ref _shortDescription, value);
    }

    public string Publisher
    {
        get => _publisher;
        set => SetProperty(ref _publisher, value);
    }

    public string ProductCode
    {
        get => _productCode;
        set => SetProperty(ref _productCode, value);
    }

    public bool Crypto
    {
        get => _crypto;
        set => SetProperty(ref _crypto, value);
    }

    public TitleType Type => (TitleType)(Convert.ToUInt64(TitleId, 16) >> 32);

    public string RegionSortKey
    {
        get
        {
            if (string.IsNullOrEmpty(_productCode) || _productCode.Length < 7) return "Z";
            try
            {
                int dashIndex = _productCode.IndexOf('-', 4);
                if (dashIndex < 0 || dashIndex + 4 >= _productCode.Length) return "Z";

                char regionChar = _productCode[dashIndex + 4];
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
        "zcci" => ["CCI"],
        _ => []
    };

    protected override string FormatSize(long bytes) => PickPack.Disk.ETC.FileSize.FormatSize(bytes);
}