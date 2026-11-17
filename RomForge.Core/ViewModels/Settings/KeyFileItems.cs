using Common.Localization;
using NSW.Core;

namespace RomForge.Core.ViewModels.Settings;

public static class KeyFileItems
{
    public static KeyFilePathItemViewModel SwitchProdKeys(KeyFilesConfig config) => new("prod.keys", "prod.keys", $"prod.keys|prod.keys|{Loc.S.FilterAllFiles}|*.*", config, () => config.SwitchProdKeysPath, value => config.SwitchProdKeysPath = value, () => KeySetProvider.Instance.KeySet != null);

    public static KeyFilePathItemViewModel WiiUKeys(KeyFilesConfig config) => new("keys.txt", "keys.txt", $"keys.txt|keys.txt|{Loc.S.FilterAllFiles}|*.*", config, () => config.WiiUKeysPath, value => config.WiiUKeysPath = value);

    public static KeyFilePathItemViewModel N3dsAesKeys(KeyFilesConfig config) => new("aes_keys.txt", "aes_keys.txt", $"aes_keys.txt|aes_keys.txt|{Loc.S.FilterAllFiles}|*.*", config, () => config.N3dsAesKeysPath, value => config.N3dsAesKeysPath = value);

    public static KeyFilePathItemViewModel N3dsBoot9(KeyFilesConfig config) => new("boot9.bin", "boot9.bin", $"boot9.bin|boot9.bin|{Loc.S.FilterAllFiles}|*.*", config, () => config.N3dsBoot9Path, value => config.N3dsBoot9Path = value);

    public static KeyFilePathItemViewModel N3dsSeedDb(KeyFilesConfig config) => new("seeddb.bin", "seeddb.bin", $"seeddb.bin|seeddb.bin|{Loc.S.FilterAllFiles}|*.*", config, () => config.N3dsSeedDbPath, value => config.N3dsSeedDbPath = value);
}