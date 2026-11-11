using N3DS.Core.Crypto;
using Common.Localization;
using RomForge.Core;
using RomForge.Core.ViewModels;
using RomForge.Core.ViewModels.Settings;
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
        KeyOverlay = new KeyOverlayViewModel("/Assets/Images/3DS.png", Loc.S.MsgNo3dsKeyFilesTitle, Loc.S.MsgNo3dsKeyFilesDesc,
                    [KeyFileItems.N3dsAesKeys(keyFiles), KeyFileItems.N3dsBoot9(keyFiles), KeyFileItems.N3dsSeedDb(keyFiles)], () => KeyStorePaths.HasUsableKeys);

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