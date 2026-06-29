using RomForge.Core;
using static System.Resources.ResXFileRef;

namespace RomForge.ViewModels.Switch;

public class SwitchMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; }

    public MergeMainViewModel MergeVM { get; }

    public ConverterMainViewModel ConverterVM { get; }

    public KeygenMainViewModel KeygenVM { get; }

    public SwitchMainViewModel(AppConfig config)
    {
        RepackVM = new RepackMainViewModel();
        MergeVM = new MergeMainViewModel(config);
        ConverterVM = new ConverterMainViewModel(config);
        KeygenVM = new KeygenMainViewModel();

        Tools.Add(RepackVM);
        Tools.Add(MergeVM);
        Tools.Add(ConverterVM);
        Tools.Add(KeygenVM);

        InitializeMultiTools();
    }
}