using RomForge.Core;

namespace RomForge.ViewModels.PS1;

public class PS1MainViewModel : MultiToolTabViewModel
{
    public PackingMainViewModel PackingVM { get; }

    public PS1MainViewModel(AppConfig config)
    {
        PackingVM = new PackingMainViewModel(config);

        Tools.Add(PackingVM);

        InitializeMultiTools();
    }
}