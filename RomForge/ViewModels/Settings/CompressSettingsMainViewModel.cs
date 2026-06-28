using Common.WPF.ViewModels;
using RomForge.Core;

namespace RomForge.ViewModels.Settings;

public class CompressSettingsMainViewModel(AppConfig config) : ToolTabViewModel
{
    public record ChdmanCompressionOption(string Value, string Display);

    public IReadOnlyList<ChdmanCompressionOption> ChdmanCompressionOptions { get; } =
        [
        new("zlib", "호환 (zlib) - PS2 AetherSX2 등 에뮬 호환"),
        new("zstd", "권장 (zstd) - 대부분 권장 압축/해제 최고 속도 준수한 압축율"),
        new("lzma", "고압축 (lzma) - 최고 압축율"),
    ];

    public ChdmanCompressionOption ChdmanCompression
    {
        get => ChdmanCompressionOptions.First(x => x.Value == config.Chdman.Compression);
        set { config.Chdman.Compression = value.Value; OnPropertyChanged(); }
    }

    public double SwitchCompressLevel
    {
        get => config.Switch.CompressLevel;
        set { config.Switch.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    public bool SwitchIsValidationEnabled
    {
        get => config.Switch.VerifyCompress;
        set { config.Switch.VerifyCompress = value; OnPropertyChanged(); }
    }

    public bool SwitchUseBlockMode
    {
        get => config.Switch.UseBlockMode;
        set
        {
            config.Switch.UseBlockMode = value;
            if (value) config.Switch.UseBlocklessMode = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SwitchUseBlocklessMode));
        }
    }

    public bool SwitchUseBlocklessMode
    {
        get => config.Switch.UseBlocklessMode;
        set
        {
            config.Switch.UseBlocklessMode = value;
            if (value) config.Switch.UseBlockMode = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SwitchUseBlockMode));
        }
    }

    public double AzaharCompressLevel
    {
        get => config.Azahar.CompressLevel;
        set { config.Azahar.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    public double DolphinCompressLevel
    {
        get => config.Dolphin.CompressLevel;
        set { config.Dolphin.CompressLevel = (int)value; OnPropertyChanged(); }
    }
}