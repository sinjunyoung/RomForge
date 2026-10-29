using N3DS.Core.Enums;
using N3DS.Core.Models;
using PickPack.Disk.Helpers;
using RomForge.Core.ViewModels;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Models.N3DS;

public class TitleViewModel : ViewModelBase
{
    private string? _originalShortDescription = string.Empty;
    private string? _originalPublisher = string.Empty;

    public InstalledTitle Title { get; init; } = null!;

    public string TitleId => Title.TitleId.ToUpperInvariant();

    public string SizeText => FileSize.FormatSize((long)Title.ContentSize);

    public BitmapSource? Icon { get; set { SetProperty(ref field, value); }}

    public string? ShortDescription { get; set { SetProperty(ref field, value); } } = string.Empty;

    public string? Publisher { get; set { SetProperty(ref field, value); } } = string.Empty;

    public bool ShortDescriptionChanged => !string.Equals(ShortDescription, _originalShortDescription, StringComparison.Ordinal);

    public bool PublisherChanged => !string.Equals(Publisher, _originalPublisher, StringComparison.Ordinal);

    public string FilePath { get; set { SetProperty(ref field, value); } } = string.Empty;

    public string? ProductCode
    {
        get;
        set
        {
            SetProperty(ref field, value);
            Region = GetRegionFromProductCode(value);
            LocaleRegionCode = GetLocaleRegionFromProductCode(value);
        }
    }

    public string Region { get; set { SetProperty(ref field, value); } } = string.Empty;

    public LocaleRegion LocaleRegionCode { get; set { SetProperty(ref field, value); } } = LocaleRegion.None;

    public LocaleLanguage ForcedLanguage { get; set { SetProperty(ref field, value); } } = LocaleLanguage.None;

    public IReadOnlyList<LocaleLanguage> AvailableLanguages { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(LanguageCandidates)); } } = [];

    public IReadOnlyList<LocaleLanguage> LanguageCandidates => [LocaleLanguage.None, .. AvailableLanguages];

    public bool Crypto { get; set { SetProperty(ref field, value); } } = false;

    public double Progress { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(ProgressText)); } }

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

    public void CommitOriginalValues()
    {
        _originalShortDescription = ShortDescription;
        _originalPublisher = Publisher;
    }

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