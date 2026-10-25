using RomForge.Core.ViewModels;

namespace RomForge.ViewModels.WiiU;

public class WiiUMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public WiiUMainViewModel()
    {
        Tools.Add(RepackVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }
}