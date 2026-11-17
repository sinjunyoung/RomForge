using RomForge.Core;
using Common.Localization;
using RomForge.Core.ViewModels;
using RomForge.Core.ViewModels.Settings;
using WiiU.Core.Services;

namespace RomForge.ViewModels.WiiU;

public class WiiUMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public KeyOverlayViewModel KeyOverlay { get; }

    public WiiUMainViewModel()
    {
        var keyFiles = AppConfig.Instance.KeyFiles;

        KeyOverlay = new KeyOverlayViewModel("/Assets/Images/WiiU.png", () => Loc.S.MsgNoWiiUKeysTitle, () => Loc.S.MsgNoWiiUKeysDesc, [KeyFileItems.WiiUKeys(keyFiles)], () => WiiUKeyLocator.Exists);
        keyFiles.PropertyChanged += (_, _) => RefreshKeysStatus();

        Tools.Add(RepackVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }

    public void RefreshKeysStatus() => KeyOverlay.Refresh();
}