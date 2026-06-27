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

    public ICommand ClearCommand { get; }

    public ICommand CalculateHashCommand { get; }

    private IPatchViewModel? SelectedPatchVM => SelectedTool as IPatchViewModel;

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
            execute: _ =>
            {
                var path = SelectedPatchVM?.SourcePath;
                if (!string.IsNullOrEmpty(path))
                    _navigateToHashAction?.Invoke(path);
            },
            canExecute: _ => !string.IsNullOrEmpty(SelectedPatchVM?.SourcePath) && _navigateToHashAction != null
        );

        Tools.Add(NormalVM);
        Tools.Add(ArcadeVM);

        InitializeMultiTools();
    }

    private async Task RunAsync()
    {
        using (BeginWork())
        {
            if (SelectedPatchVM != null)
                await SelectedPatchVM.RunAsync();
        }
    }

    private void Cancel() => SelectedPatchVM?.Cancel();

    private void Clear() => SelectedPatchVM?.Clear();
}