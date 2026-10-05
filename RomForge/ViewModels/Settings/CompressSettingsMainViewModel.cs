using Common.WPF.ViewModels;
using RomForge.Core;

namespace RomForge.ViewModels.Settings;

public class CompressSettingsMainViewModel() : ToolTabViewModel
{
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            _selectedTabIndex = value;
            OnPropertyChanged();
        }
    }

    public record ChdmanCompressionOption(string Value, string Display);

    public IReadOnlyList<ChdmanCompressionOption> ChdmanCompressionOptions { get; } =
        [
        new("zlib", "호환 (zlib) - PS2 AetherSX2 등 에뮬 호환"),
        new("zstd", "권장 (zstd) - 대부분 권장 압축/해제 최고 속도 준수한 압축율"),
        new("lzma", "고압축 (lzma) - 최고 압축율"),
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

    public bool SwitchIsValidationEnabled
    {
        get => AppConfig.Instance.Switch.VerifyCompress;
        set { AppConfig.Instance.Switch.VerifyCompress = value; OnPropertyChanged(); }
    }

    public bool SwitchUseBlockMode
    {
        get => AppConfig.Instance.Switch.UseBlockMode;
        set
        {
            AppConfig.Instance.Switch.UseBlockMode = value;

            if (value)
                AppConfig.Instance.Switch.UseBlocklessMode = false;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SwitchUseBlocklessMode));
        }
    }

    public bool SwitchUseBlocklessMode
    {
        get => AppConfig.Instance.Switch.UseBlocklessMode;
        set
        {
            AppConfig.Instance.Switch.UseBlocklessMode = value;

            if (value) 
                AppConfig.Instance.Switch.UseBlockMode = false;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SwitchUseBlockMode));
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
}