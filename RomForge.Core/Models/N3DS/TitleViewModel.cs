using N3DS.Core.Enums;
using N3DS.Core.Models;
using PickPack.Disk.Helpers;
using RomForge.Core.ViewModels;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Models.N3DS;

public class TitleViewModel : ViewModelBase
{
    private BitmapSource? _icon;
    private string _filePath = string.Empty;
    private string _shortDescription = string.Empty;
    private string _publisher = string.Empty;
    private string _originalShortDescription = string.Empty;
    private string _originalPublisher = string.Empty;
    private string _productCode = string.Empty;
    private string _region = string.Empty;
    private LocaleRegion _localeRegionCode = LocaleRegion.None;
    private LocaleLanguage _forcedLanguage = LocaleLanguage.None;
    private IReadOnlyList<LocaleLanguage> _availableLanguages = [];
    private bool _crypto = false;
    private double _progress;

    public InstalledTitle Title { get; init; } = null!;

    public string TitleId => Title.TitleId.ToUpperInvariant();

    public string SizeText => FileSize.FormatSize((long)Title.ContentSize);

    public BitmapSource? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    public string ShortDescription
    {
        get => _shortDescription;
        set { _shortDescription = value; OnPropertyChanged(); }
    }

    public string Publisher
    {
        get => _publisher;
        set { _publisher = value; OnPropertyChanged(); }
    }

    public bool ShortDescriptionChanged => !string.Equals(_shortDescription, _originalShortDescription, StringComparison.Ordinal);

    public bool PublisherChanged => !string.Equals(_publisher, _originalPublisher, StringComparison.Ordinal);

    public void CommitOriginalValues()
    {
        _originalShortDescription = _shortDescription;
        _originalPublisher = _publisher;
    }

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); }
    }

    public string ProductCode
    {
        get => _productCode;
        set
        {
            _productCode = value;
            OnPropertyChanged();
            Region = GetRegionFromProductCode(value);
            LocaleRegionCode = GetLocaleRegionFromProductCode(value);
        }
    }

    public string Region
    {
        get => _region;
        set { _region = value; OnPropertyChanged(); }
    }

    public LocaleRegion LocaleRegionCode
    {
        get => _localeRegionCode;
        private set { _localeRegionCode = value; OnPropertyChanged(); }
    }

    public LocaleLanguage ForcedLanguage
    {
        get => _forcedLanguage;
        set { _forcedLanguage = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<LocaleLanguage> AvailableLanguages
    {
        get => _availableLanguages;
        set
        {
            _availableLanguages = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LanguageCandidates));
        }
    }

    public IReadOnlyList<LocaleLanguage> LanguageCandidates =>
        [LocaleLanguage.None, .. AvailableLanguages];

    public bool Crypto
    {
        get => _crypto;
        set { _crypto = value; OnPropertyChanged(); }
    }

    public double Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressText));
        }
    }

    public string ProgressText => $"{Progress:F0}";

    public string TypeLabel => Title.Type switch
    {
        TitleType.Application => "본편",
        TitleType.SystemApplication => "시스템",
        TitleType.Patch => "업데이트",
        TitleType.DlcContent => "DLC",
        _ => "기타",
    };

    public SolidColorBrush TypeBadgeColor => Title.Type switch
    {
        TitleType.Application => new SolidColorBrush(Color.FromRgb(0x4F, 0x8E, 0xF7)),
        TitleType.SystemApplication => new SolidColorBrush(Color.FromRgb(0xF7, 0x9A, 0x3D)),
        TitleType.Patch => new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C)),
        TitleType.DlcContent => new SolidColorBrush(Color.FromRgb(0xC9, 0x7B, 0xF7)),
        _ => new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x6A)),
    };

    private static string GetRegionFromProductCode(string productCode)
    {
        if (string.IsNullOrEmpty(productCode) || productCode.Length < 7)
            return string.Empty;

        char region = productCode[productCode.IndexOf('-', 4) + 4];

        return region switch
        {
            'J' => "일본",
            'E' => "북미",
            'P' => "유럽",
            'K' => "한국",
            'A' => "세계",
            'C' => "중국",
            'T' => "대만",
            'W' => "세계",
            'G' => "독일",
            'F' => "프랑스",
            'S' => "스페인",
            'I' => "이탈리아",
            'H' => "네덜란드",
            'R' => "러시아",
            'U' => "북미",
            'N' => "북미",
            'X' => "세계",
            'Z' => "세계",
            _ => "알 수 없음"
        };
    }

    private static LocaleRegion GetLocaleRegionFromProductCode(string productCode)
    {
        if (string.IsNullOrEmpty(productCode) || productCode.Length < 7)
            return LocaleRegion.None;

        char region = productCode[productCode.IndexOf('-', 4) + 4];

        return region switch
        {
            'J' => LocaleRegion.JPN,
            'E' or 'U' or 'N' => LocaleRegion.USA,
            'P' or 'G' or 'F' or 'S' or 'I' or 'H' or 'R' => LocaleRegion.EUR,
            'K' => LocaleRegion.KOR,
            'A' or 'W' or 'X' or 'Z' => LocaleRegion.None,
            'C' => LocaleRegion.CHN,
            'T' => LocaleRegion.TWN,
            _ => LocaleRegion.None,
        };
    }
}