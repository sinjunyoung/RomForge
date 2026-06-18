using Common.WPF.ViewModels;
using RomForge.Core;

namespace RomForge.ViewModels.Settings;

public class SettingsMainViewModel(AppConfig config) : ToolTabViewModel
{
    public PatchSettingsViewModel Patch { get; } = new PatchSettingsViewModel(config);

    public CompressSettingsViewModel Compress { get; } = new CompressSettingsViewModel(config);
}