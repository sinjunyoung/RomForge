using RomForge.Core;
using RomForge.Core.ViewModels;

namespace RomForge.ViewModels.Settings;

public class KeyFileSettingsMainViewModel : ToolTabViewModel
{
    public IReadOnlyList<KeyFilePathItemViewModel> Items { get; }

    public KeyFileSettingsMainViewModel()
    {
        var config = AppConfig.Instance.KeyFiles;

        Items =
        [
            new("Switch  prod.keys", "prod.keys", "prod.keys|*.keys|모든 파일|*.*", false, () => config.SwitchProdKeysPath, value => config.SwitchProdKeysPath = value),
            new("Wii U  keys.txt", "keys.txt", "keys.txt|*.txt|모든 파일|*.*", false, () => config.WiiUKeysPath, value => config.WiiUKeysPath = value),
            new("3DS  aes_keys.txt", "aes_keys.txt", "aes_keys.txt|*.txt|모든 파일|*.*", false, () => config.N3dsAesKeysPath, value => config.N3dsAesKeysPath = value),
            new("3DS  boot9.bin", "boot9.bin", "boot9.bin|*.bin|모든 파일|*.*", true, () => config.N3dsBoot9Path, value => config.N3dsBoot9Path = value),
            new("3DS  seeddb.bin", "seeddb.bin", "seeddb.bin|*.bin|모든 파일|*.*", true, () => config.N3dsSeedDbPath, value => config.N3dsSeedDbPath = value)
        ];

        config.PropertyChanged += (_, _) =>
        {
            foreach (var item in Items)
                item.Refresh();
        };
    }
}