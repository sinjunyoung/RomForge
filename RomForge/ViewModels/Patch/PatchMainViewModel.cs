using RomForge.Helpers;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class PatchMainViewModel : MultiToolTabViewModel
{
    private readonly Core.AppConfig _config;
    private readonly Action<string> _navigateToHashAction;

    public NormalPatchMainViewModel NormalVM { get; }
    public ArcadePatchMainViewModel ArcadeVM { get; }

    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CalculateHashCommand { get; }
    public PatchMainViewModel(Core.AppConfig config, Action<string> navigateToHashAction)
    {
        _config = config;
        _navigateToHashAction = navigateToHashAction;
        NormalVM = new NormalPatchMainViewModel(_config);

        ArcadeVM = new ArcadePatchMainViewModel();        

        RunCommand = new RelayCommand(async _ => await RunAsync());
        CancelCommand = new RelayCommand(_ => Cancel());
        ClearCommand = new RelayCommand(_ => Clear());
        CalculateHashCommand = new RelayCommand(
            execute: _ => _navigateToHashAction?.Invoke(NormalVM.SourcePath),
            canExecute: _ => !string.IsNullOrEmpty(NormalVM.SourcePath) && _navigateToHashAction != null
        );

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