namespace RomForge.ViewModels._3DS;

public class _3DSMainViewModel : MultiToolTabViewModel
{
    public InstallerMainViewModel InstallerVM { get; }
    public ConverterMainViewModel ConverterVM { get; }

    public _3DSMainViewModel()
    {
        InstallerVM = new InstallerMainViewModel();
        ConverterVM = new ConverterMainViewModel();

        Tools.Add(InstallerVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }
}