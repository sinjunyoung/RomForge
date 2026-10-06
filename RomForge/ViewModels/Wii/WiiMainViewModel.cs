namespace RomForge.ViewModels.Wii;

public class WiiMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public WiiMainViewModel()
    {
        Tools.Add(RepackVM);

        InitializeMultiTools();
    }
}