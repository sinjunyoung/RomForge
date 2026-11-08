using RomForge.Core;
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

        KeyOverlay = new KeyOverlayViewModel("/Assets/Images/WiiU.png", "keys.txt 파일이 없습니다", "keys.txt가 필요합니다. exe와 같은 폴더에 넣거나 직접 지정해주세요.", [KeyFileItems.WiiUKeys(keyFiles)], () => WiiUKeyLocator.Exists);
        keyFiles.PropertyChanged += (_, _) => RefreshKeysStatus();

        Tools.Add(RepackVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }

    public void RefreshKeysStatus() => KeyOverlay.Refresh();
}