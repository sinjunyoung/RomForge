using Common.WPF.ViewModels;
using RomForge.Core;

namespace RomForge.ViewModels.Settings;

public class PSSettingsMainViewModel() : ToolTabViewModel
{
    public double CompressLevel
    {
        get => AppConfig.Instance.PS1.CompressLevel;
        set { AppConfig.Instance.PS1.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    public bool UseGameIdMode
    {
        get => AppConfig.Instance.PS1.UseGameIdMode;
        set
        {
            AppConfig.Instance.PS1.UseGameIdMode = value;

            if (value)
                AppConfig.Instance.PS1.UseFileNameMode = false;

            OnPropertyChanged();
            OnPropertyChanged(nameof(UseFileNameMode));
        }
    }

    public bool UseFileNameMode
    {
        get => AppConfig.Instance.PS1.UseFileNameMode;
        set
        {
            AppConfig.Instance.PS1.UseFileNameMode = value;

            if (value)
                AppConfig.Instance.PS1.UseGameIdMode = false;

            OnPropertyChanged();
            OnPropertyChanged(nameof(UseGameIdMode));
        }
    }

    public bool UseUpperCase
    {
        get => AppConfig.Instance.PS1.UseUpperCase;
        set
        {
            AppConfig.Instance.PS1.UseUpperCase = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExtensionText));
        }
    }

    public string ExtensionText => UseUpperCase ? "PBP" : "pbp";
}