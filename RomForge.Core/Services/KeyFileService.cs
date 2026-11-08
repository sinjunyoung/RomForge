using N3DS.Core.Crypto;
using NSW.Core;
using WiiU.Core.Services;

namespace RomForge.Core.Services;

public static class KeyFileService
{
    public static void Apply(KeyFilesConfig config)
    {
        KeySetProvider.Instance.SetCustomKeysPath(config.SwitchProdKeysPath);

        WiiUKeyLocator.CustomPath = config.WiiUKeysPath;

        KeyStorePaths.CustomAesKeys = config.N3dsAesKeysPath;
        KeyStorePaths.CustomBoot9 = config.N3dsBoot9Path;
        KeyStorePaths.CustomSeedDb = config.N3dsSeedDbPath;

        try
        {
            KeyStoreProvider.Instance.Reload();
        }
        catch { }
    }
}