using RomForge.Core;

namespace RomForge.ViewModels.PSP;

public class PSPMainViewModel : MultiToolTabViewModel
{   
    public PSPConverterViewModel ConverterVM { get; }

    public PSPMainViewModel(AppConfig config)
    {
        ConverterVM = new PSPConverterViewModel(config);

        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }
}