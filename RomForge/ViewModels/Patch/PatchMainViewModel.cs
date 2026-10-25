using RomForge.Core.UI.Command;
using RomForge.Core.Services.Patch;
using System.Windows.Input;
using RomForge.Core.ViewModels;

namespace RomForge.ViewModels.Patch;

public class PatchMainViewModel : MultiToolTabViewModel
{
    private readonly Action<string> _navigateToHashAction;

    public NormalPatchMainViewModel NormalVM { get; } = new();

    public ArcadePatchMainViewModel ArcadeVM { get; } = new();

    public Pc98PatchMainViewModel Pc98VM { get; } = new();

    public ICommand RunCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand CalculateHashCommand { get; }

    private IPatchViewModel? SelectedPatchVM => SelectedTool as IPatchViewModel;

    public PatchMainViewModel(Action<string> navigateToHashAction)
    {
        _navigateToHashAction = navigateToHashAction;
        RunCommand = new RelayCommand(async _ => await RunAsync(), _=> SelectedPatchVM?.CanRun() ?? false);
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
        Tools.Add(Pc98VM);

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

    public override void Cancel() => SelectedPatchVM?.Cancel();

    private void Clear() => SelectedPatchVM?.Clear();
}