using _3DS.Core.Enums;
using Common.WPF.ViewModels;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels._3DS;

public class DecryptorFileItem : ViewModelBase
{
    private BitmapSource? _icon;
    private string _titleId = string.Empty;
    private string _shortDescription = string.Empty;
    private string _publisher = string.Empty;
    private string _productCode = string.Empty;
    private string _status = string.Empty;
    private bool _crypto;
    private int _progress;

    public int No { get; set; }

    public string FilePath { get; }

    public string FileName => Path.GetFileNameWithoutExtension(FilePath);

    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();

    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public string FileSize { get; }

    public long FileSizeBytes { get; }

    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public BitmapSource? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    public string TitleId
    {
        get => _titleId;
        set { _titleId = value; OnPropertyChanged(); }
    }

    public string ShortDescription
    {
        get => string.IsNullOrEmpty(_shortDescription) ? TitleId : _shortDescription;
        set { _shortDescription = value; OnPropertyChanged(); }
    }

    public string Publisher
    {
        get => _publisher;
        set { _publisher = value; OnPropertyChanged(); }
    }

    public string ProductCode
    {
        get => _productCode;
        set { _productCode = value; OnPropertyChanged(); }
    }

    public bool Crypto
    {
        get => _crypto;
        set { _crypto = value; OnPropertyChanged(); }
    }

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

    public Brush ExtensionBackground => Extension switch
    {
        "3ds" => MakeBrush("#FFE094"),
        "cci" => MakeBrush("#FFCE73"),
        "cia" => MakeBrush("#C96F2C"),
        _ => Brushes.Transparent
    };

    public DecryptorFileItem(string filePath)
    {
        FilePath = filePath;
        FileSizeBytes = new FileInfo(filePath).Length;
        FileSize = PickPack.Disk.ETC.FileSize.FormatSize(FileSizeBytes);
    }

    private static SolidColorBrush MakeBrush(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(c);
        brush.Freeze();

        return brush;
    }
}