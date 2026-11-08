using RomForge.Core;
using RomForge.Core.ViewModels;
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

        KeyOverlay = new KeyOverlayViewModel("/Assets/Images/WiiU.png", "keys.txt 파일이 없습니다", "wud/wux 파일을 사용하려면 keys.txt가 필요합니다. keys.txt를 exe와 같은 폴더에 넣거나 직접 지정해주세요. wua 파일이나 WUP 폴더만 사용한다면 키 없이 계속할 수 있습니다.",
            "keys.txt|*.txt|모든 파일|*.*", () => WiiUKeyLocator.Exists, () => keyFiles.WiiUKeysPath, path => keyFiles.WiiUKeysPath = path, canSkip: true);

        keyFiles.PropertyChanged += (_, _) => RefreshKeysStatus();

        Tools.Add(RepackVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }

    public void RefreshKeysStatus() => KeyOverlay.Refresh();
}