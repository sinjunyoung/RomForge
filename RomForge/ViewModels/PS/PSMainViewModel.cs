using RomForge.Core;

namespace RomForge.ViewModels.PS;

public class PS1MainViewModel : MultiToolTabViewModel
{
    public PackingMainViewModel PackingVM { get; }

    public UnpackingMainViewModel UnPackingVM { get; }

    public PSPConverterViewModel ConverterVM { get; }


    public event EventHandler RunNavigatePackingSettings;

    public PS1MainViewModel(AppConfig config)
    {
        PackingVM = new PackingMainViewModel(config);
        PackingVM.RunNavigateSettings += (sender, e) => RunNavigatePackingSettings?.Invoke(sender, e);
        UnPackingVM = new UnpackingMainViewModel();
        ConverterVM = new PSPConverterViewModel(config);

        Tools.Add(PackingVM);
        Tools.Add(UnPackingVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }
}