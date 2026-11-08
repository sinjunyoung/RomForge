using RomForge.Core;
using RomForge.Core.ViewModels;
using RomForge.Core.ViewModels.Settings;

namespace RomForge.ViewModels.Settings;

public class KeyFileSettingsMainViewModel : ToolTabViewModel
{
    public IReadOnlyList<KeyFilePathItemViewModel> SwitchItems { get; }

    public IReadOnlyList<KeyFilePathItemViewModel> WiiUItems { get; }

    public IReadOnlyList<KeyFilePathItemViewModel> N3dsItems { get; }

    public KeyFileSettingsMainViewModel()
    {
        var config = AppConfig.Instance.KeyFiles;

        SwitchItems = [KeyFileItems.SwitchProdKeys(config)];
        WiiUItems = [KeyFileItems.WiiUKeys(config)];
        N3dsItems = [KeyFileItems.N3dsAesKeys(config), KeyFileItems.N3dsBoot9(config), KeyFileItems.N3dsSeedDb(config)];
    }
}