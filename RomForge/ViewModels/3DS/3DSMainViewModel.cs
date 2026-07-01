namespace RomForge.ViewModels._3DS;

public class _3DSMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();
    public InstallerMainViewModel InstallerVM { get; } = new();
    public ConverterMainViewModel ConverterVM { get; } = new();
    public DecryptorMainViewModel DecryptorVM { get; } = new();

    public event EventHandler RunNavigateCerts;

    public _3DSMainViewModel()
    {
        InstallerVM.RunNavigateCerts += (s, e) => RunNavigateCerts?.Invoke(s, e);
        ConverterVM.RunNavigateCerts += (s, e) => RunNavigateCerts?.Invoke(s, e);

        Tools.Add(RepackVM);
        Tools.Add(InstallerVM);
        Tools.Add(ConverterVM);
        Tools.Add(DecryptorVM);

        InitializeMultiTools();
    }
}