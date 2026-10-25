using RomForge.Core.ViewModels;

namespace RomForge.ViewModels.PSVita;

public class PSVitaMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public PSVitaMainViewModel()
    {
        Tools.Add(RepackVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }
}