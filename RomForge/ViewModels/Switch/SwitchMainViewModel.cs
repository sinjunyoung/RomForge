namespace RomForge.ViewModels.Switch;

public class SwitchMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public MergeMainViewModel MergeVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public ConvertSaturnMainViewModel ConvertSaturnVM { get; } = new ();

    public KeygenMainViewModel KeygenVM { get; } = new();

    public SwitchMainViewModel()
    {
        Tools.Add(RepackVM);
        Tools.Add(MergeVM);
        Tools.Add(ConverterVM);
        Tools.Add(ConvertSaturnVM);
        Tools.Add(KeygenVM);

        InitializeMultiTools();
    }
}