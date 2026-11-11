using Common.Services;
using Common.Localization;
using N3DS.Core.Enums;
using N3DS.Core.Models;
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

    public string TypeLabel => TitleTypeText.Of(Title.Type);

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

    private static string GetRegionFromProductCode(string? productCode)
    {
        if (string.IsNullOrEmpty(productCode) || productCode.Length < 7)
            return string.Empty;

        char region = productCode[productCode.IndexOf('-', 4) + 4];

        return region switch
        {
            'J' => Loc.S.RegionJapan,
            'E' or 'U' or 'N' => Loc.S.RegionNorthAmerica,
            'P' => Loc.S.RegionEurope,
            'K' => Loc.S.RegionKorea,
            'A' or 'W' or 'X' or 'Z' => Loc.S.RegionWorld,
            'C' => Loc.S.RegionChina,
            'T' => Loc.S.RegionTaiwan,
            'G' => Loc.S.RegionGermany,
            'F' => Loc.S.RegionFrance,
            'S' => Loc.S.RegionSpain,
            'I' => Loc.S.RegionItaly,
            'H' => Loc.S.RegionNetherlands,
            'R' => Loc.S.RegionRussia,
            _ => Loc.S.LabelUnknown
        };
    }

    private static LocaleRegion GetLocaleRegionFromProductCode(string? productCode)
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