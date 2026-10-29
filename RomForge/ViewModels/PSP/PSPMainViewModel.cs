using RomForge.Core.ViewModels;

namespace RomForge.ViewModels.PSP;

public class PSPMainViewModel : MultiToolTabViewModel
{
    public PackingMainViewModel PackingVM { get; } = new();

    public UnpackingMainViewModel UnPackingVM { get; } = new();

    public ConverterViewModel ConverterVM { get; } = new();


    public event EventHandler? RunNavigatePackingSettings;

    public PSPMainViewModel()
    {
        PackingVM.RunNavigateSettings += (sender, e) => RunNavigatePackingSettings?.Invoke(sender, e);

        Tools.Add(PackingVM);
        Tools.Add(UnPackingVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }
}