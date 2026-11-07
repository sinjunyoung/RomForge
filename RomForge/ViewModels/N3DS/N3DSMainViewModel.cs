using RomForge.Core.ViewModels;
using System.ComponentModel;

namespace RomForge.ViewModels.N3DS;

public class N3DSMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public InstallerMainViewModel InstallerVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public DecryptorMainViewModel DecryptorVM { get; } = new();

    public event EventHandler? RunNavigateCerts;

    public N3DSMainViewModel()
    {
        InstallerVM.RunNavigateCerts += (s, e) => RunNavigateCerts?.Invoke(s, e);
        ConverterVM.RunNavigateCerts += (s, e) => RunNavigateCerts?.Invoke(s, e);

        InstallerVM.PropertyChanged += InstallerVM_PropertyChanged;

        Tools.Add(RepackVM);
        Tools.Add(InstallerVM);
        Tools.Add(ConverterVM);
        Tools.Add(DecryptorVM);

        InitializeMultiTools();
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