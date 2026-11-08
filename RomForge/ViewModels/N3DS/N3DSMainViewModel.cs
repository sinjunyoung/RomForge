using N3DS.Core.Crypto;
using RomForge.Core;
using RomForge.Core.ViewModels;
using System.ComponentModel;

namespace RomForge.ViewModels.N3DS;

public class N3DSMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public InstallerMainViewModel InstallerVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public DecryptorMainViewModel DecryptorVM { get; } = new();

    public KeyOverlayViewModel KeyOverlay { get; }

    private bool _keysWereAvailable;

    public event EventHandler? RunNavigateCerts;

    public N3DSMainViewModel()
    {
        var keyFiles = AppConfig.Instance.KeyFiles;

        _keysWereAvailable = KeyStorePaths.HasUsableKeys;
        KeyOverlay = new KeyOverlayViewModel("/Assets/Images/3DS.png", "aes_keys.txt 파일이 없습니다", "암호화된 3DS 파일을 다루려면 aes_keys.txt가 필요합니다. aes_keys.txt를 exe와 같은 폴더에 넣거나 직접 지정해주세요. 복호화된 파일만 사용한다면 키 없이 계속할 수 있습니다.",
            "aes_keys.txt|*.txt|모든 파일|*.*", () => KeyStorePaths.HasUsableKeys, () => keyFiles.N3dsAesKeysPath, path => keyFiles.N3dsAesKeysPath = path, canSkip: true);

        keyFiles.PropertyChanged += (_, _) => RefreshKeysStatus();
        InstallerVM.RunNavigateCerts += (s, e) => RunNavigateCerts?.Invoke(s, e);
        ConverterVM.RunNavigateCerts += (s, e) => RunNavigateCerts?.Invoke(s, e);
        InstallerVM.PropertyChanged += InstallerVM_PropertyChanged;

        Tools.Add(RepackVM);
        Tools.Add(InstallerVM);
        Tools.Add(ConverterVM);
        Tools.Add(DecryptorVM);

        InitializeMultiTools();
    }

    public void RefreshKeysStatus()
    {
        bool available = KeyStorePaths.HasUsableKeys;

        if (available && !_keysWereAvailable)
        {
            try
            {
                KeyStoreProvider.Instance.Reload();
            }
            catch { }
        }

        _keysWereAvailable = available;

        KeyOverlay.Refresh();
    }

    private void InstallerVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (SelectedViewModel != InstallerVM)
            return;

        if (e.PropertyName == nameof(HasHelpKey))
            OnPropertyChanged(nameof(HasHelpKey));

        if (e.PropertyName == nameof(HelpPageKey))
            OnPropertyChanged(nameof(HelpPageKey));
    }
}