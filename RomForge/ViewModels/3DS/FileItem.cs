using _3DS.Core.Enums;
using Common.WPF.ViewModels;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels._3DS;

public class FileItem : ViewModelBase
{
    private BitmapSource? _icon;
    private string _titleId;    
    private string _shortDescription = string.Empty;
    private string _publisher = string.Empty;
    private string _productCode = string.Empty;
    private string _status = string.Empty;
    private string _selectedTargetFormat = string.Empty;
    private int _progress;
    private bool _crypto = false;
    private int _no;

    public string FilePath { get; }

    public string FileName => Path.GetFileNameWithoutExtension(FilePath);

    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();

    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public string FileSize { get; }

    public long FileSizeBytes { get; }

    public List<string> AvailableFormats { get; private set; } = [];

    public string SelectedTargetFormat
    {
        get => _selectedTargetFormat;
        set
        {
            if (_selectedTargetFormat != value)
            {
                _selectedTargetFormat = value;
                OnPropertyChanged();
            }
        }
    }

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
        set
        {
            _productCode = value;
            OnPropertyChanged();
        }
    }

    public bool Crypto
    {
        get => _crypto;
        set { _crypto = value; OnPropertyChanged(); }
    }

    public int No
    {
        get => _no;
        set { _no = value; OnPropertyChanged(); }
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
        "3ds" => Brush("#FFE094"),
        "cci" => Brush("#FFCE73"),
        "cia" => Brush("#C96F2C"),
        "zcci" => Brush("#D48843"),

        _ => Brushes.Transparent
    };

    public FileItem(string filePath)
    {
        FilePath = filePath;
        FileSizeBytes = new FileInfo(filePath).Length;
        FileSize = PickPack.Disk.ETC.FileSize.FormatSize(FileSizeBytes);

        InitAvailableFormats();
    }

    private void InitAvailableFormats()
    {
        AvailableFormats.Clear();

        switch (Extension)
        {
            case "3ds":                
                AvailableFormats.Add("CIA");
                AvailableFormats.Add("ZCCI");
                SelectedTargetFormat = "CIA";
                break;

            case "cci":                
                AvailableFormats.Add("CIA");
                AvailableFormats.Add("ZCCI");
                SelectedTargetFormat = "CIA";
                break;

            case "cia":
                AvailableFormats.Add("CCI");
                AvailableFormats.Add("ZCCI");
                SelectedTargetFormat = "CCI";
                break;

            case "zcci":
                AvailableFormats.Add("CCI");
                SelectedTargetFormat = "CCI";
                break;

            default:
                SelectedTargetFormat = "미지원";
                break;
        }
    }

    private static SolidColorBrush Brush(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(c);

        brush.Freeze();

        return brush;
    }
}