namespace RomForge.ViewModels.Settings;

public class SettingsMainViewModel : MultiToolTabViewModel
{
    public GeneralSettingsMainViewModel General { get; } = new();

    public PatchSettingsMainViewModel Patch { get; } = new();

    public CompressSettingsMainViewModel Compress { get; } = new();

    public PSSettingsMainViewModel PS1 { get; } = new();

    public SettingsMainViewModel()
    {
        Tools.Add(General);
        Tools.Add(Patch);
        Tools.Add(Compress);
        Tools.Add(PS1);

        InitializeMultiTools();
    }
}