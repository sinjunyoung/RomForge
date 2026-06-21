using RomForge.Core;

namespace RomForge.ViewModels.Switch;

public class SwitchMainViewModel : MultiToolTabViewModel
{

    public RepackMainViewModel RepackVM { get; }

    public MergeMainViewModel MergeVM { get; }

    public KeygenMainViewModel KeygenVM { get; }

    public SwitchMainViewModel(AppConfig config)
    {
        RepackVM = new RepackMainViewModel();
        MergeVM = new MergeMainViewModel(config);
        KeygenVM = new KeygenMainViewModel();

        Tools.Add(RepackVM);
        Tools.Add(MergeVM);
        Tools.Add(KeygenVM);

        InitializeMultiTools();
    }
}