using RomForge.Helpers;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class PatchMainViewModel : MultiToolTabViewModel
{
    private readonly Core.AppConfig _config;

    public NormalPatchMainViewModel NormalVM { get; }
    public ArcadePatchMainViewModel ArcadeVM { get; }

    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCommand { get; }

    public PatchMainViewModel(Core.AppConfig config)
    {
        _config = config;
        NormalVM = new NormalPatchMainViewModel(_config);
        ArcadeVM = new ArcadePatchMainViewModel();

        RunCommand = new RelayCommand(async _ => await RunAsync());
        CancelCommand = new RelayCommand(_ => Cancel());
        ClearCommand = new RelayCommand(_ => Clear());

        Tools.Add(NormalVM);
        Tools.Add(ArcadeVM);

        InitializeMultiTools();
    }

    private async Task RunAsync()
    {
        using (BeginWork())
        {
            switch (SubTabIndex)
            {
                case 0:
                    await NormalVM.RunAsync();
                    break;
                case 1:
                    await ArcadeVM.RunAsync();
                    break;
            }
        }
    }

    private void Cancel()
    {
        switch (SubTabIndex)
        {
            case 0:
                NormalVM.Cancel();
                break;
            case 1:
                ArcadeVM.Cancel();
                break;
        }
    }

    private void Clear()
    {
        switch (SubTabIndex)
        {
            case 0:
                NormalVM.Clear();
                break;
            case 1:
                ArcadeVM.Clear();
                break;
        }
    }
}