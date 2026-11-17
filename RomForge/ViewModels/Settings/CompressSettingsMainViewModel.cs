using Common.Localization;
using Common.Models;
using RomForge.Core;
using RomForge.Core.ViewModels;

namespace RomForge.ViewModels.Settings;

public class CompressSettingsMainViewModel : ToolTabViewModel
{
    public int SelectedTabIndex { get; set { SetProperty(ref field, value); } }

    public record ChdmanCompressionOption(string Value, string Display);

    public IReadOnlyList<ChdmanCompressionOption> ChdmanCompressionOptions { get; } =
        [
        new("zlib", Loc.S.OptionChdmanZlib),
        new("zstd", Loc.S.OptionChdmanZstd),
        new("lzma", Loc.S.OptionChdmanLzma),
    ];

    public ChdmanCompressionOption ChdmanCompression
    {
        get => ChdmanCompressionOptions.First(x => x.Value == AppConfig.Instance.Chdman.Compression);
        set { AppConfig.Instance.Chdman.Compression = value.Value; OnPropertyChanged(); }
    }

    public double SwitchCompressLevel
    {
        get => AppConfig.Instance.Switch.CompressLevel;
        set { AppConfig.Instance.Switch.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    public SwitchCompressionMode SwitchCompressionMode
    {
        get => AppConfig.Instance.Switch.CompressionMode;
        set
        {
            if (AppConfig.Instance.Switch.CompressionMode == value)
                return;

            AppConfig.Instance.Switch.CompressionMode = value;
            OnPropertyChanged();
        }
    }

    public double AzaharCompressLevel
    {
        get => AppConfig.Instance.Azahar.CompressLevel;
        set { AppConfig.Instance.Azahar.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    public double WiiGCCompressLevel
    {
        get => AppConfig.Instance.WiiGC.CompressLevel;
        set { AppConfig.Instance.WiiGC.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    public CompressSettingsMainViewModel()
    {
        AppConfig.Instance.Switch.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SwitchConfig.CompressionMode))
                OnPropertyChanged(nameof(SwitchCompressionMode));
        };
    }
}