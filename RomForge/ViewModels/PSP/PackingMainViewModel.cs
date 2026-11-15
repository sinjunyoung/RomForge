using RomForge.Core;
using Common.Localization;
using RomForge.Core.UI.Command;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RomForge.Core.Services;
using RomForge.Core.Services.PSP;
using RomForge.Core.ViewModels.PSP;
using Common.Models;
using Common.Services;

namespace RomForge.ViewModels.PSP;

public class PackingMainViewModel : ProgressToolTabViewModel
{
    private CancellationTokenSource _cts = new();

    public DiscListViewModel Files { get; }

    public PackingImagesViewModel Images { get; }

    public BgmViewModel Bgm { get; }

    public PopsOptionsViewModel PopsOptions { get; }

    public string GameTitle { get; set { SetProperty(ref field, value); } } = string.Empty;

    public bool CanEditPopsConfig => IsUnlocked && !Files.HasPresetConfig;

    public bool CanAdd => !IsLocked && Files.FileItems.Count < DiscListViewModel.MaxItems;

    public bool CanRun => !IsLocked && Files.FileItems.Count > 0 && !Images.IsDownloading;

    public override string? HelpPageKey => "psp-pbp-convert";

    public ICommand RunCommand { get; }

    public ICommand SettingsCommand { get; }

    public event EventHandler? RunNavigateSettings;

    public PackingMainViewModel()
    {
        Files = new DiscListViewModel(AppendLog);
        Images = new PackingImagesViewModel();
        Bgm = new BgmViewModel(AppendLog);
        PopsOptions = new PopsOptionsViewModel();

        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => CanRun);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsLocked);
        SettingsCommand = new RelayCommand(async _ => RunNavigateSettings?.Invoke(this, EventArgs.Empty), _ => !IsLocked);

        Files.FileItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanAdd));
        Files.Changed += OnFilesChanged;
        Files.Cleared += OnFilesCleared;

        Images.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PackingImagesViewModel.IsDownloading))
                return;

            OnPropertyChanged(nameof(CanRun));
            Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        };

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsLocked))
                OnPropertyChanged(nameof(CanAdd));
        };
    }

    private void OnFilesChanged(object? sender, EventArgs e)
    {
        _ = UpdateCoverArtAsync();
        OnPropertyChanged(nameof(CanEditPopsConfig));
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnFilesCleared(object? sender, EventArgs e)
    {
        Images.ResetAll();
        Bgm.Reset();
        OnPropertyChanged(nameof(CanEditPopsConfig));
    }

    private async Task UpdateCoverArtAsync()
    {
        var title = await Images.UpdateFromGameIdAsync(Files.Primary is { IsGameIdResolved: true } primary ? primary.GameId : null);

        if (title != null)
            GameTitle = title;
    }

    private async Task RunAsync()
    {
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        if (Files.FileItems.Count == 0)
        {
            AppendLog(Loc.S.LogNoFilesAdded, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (Files.FileItems.Any(i => !i.IsGameIdResolved))
        {
            AppendLog(Loc.S.LogGameIdRecognizeError, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        using (BeginWork())
        {
            var orderedItems = Files.FileItems.OrderBy(i => i.No).ToList();
            var gameTitle = string.IsNullOrWhiteSpace(GameTitle) ? DiscListSorter.GuessTitle(orderedItems[0].FilePath) : GameTitle;
            var mainGameId = orderedItems[0].GameId;
            var assets = Images.BuildAssets(Bgm.Bytes);
            var plan = PackingJobRunner.PlanOutput(orderedItems[0].FilePath, gameTitle, mainGameId);
            var popsConfig = PopsOptions.BuildConfig(orderedItems[0].PresetConfigBytes);

            try
            {
                AppendLog(Loc.S.LogJobStartPacking(gameTitle, mainGameId, orderedItems.Count), LogLevel.Highlight);
                await PackingJobRunner.RunAsync(orderedItems, gameTitle, mainGameId, plan, AppConfig.Instance.PS1.CompressLevel, assets, popsConfig, BuildProgressReporter(), _cts.Token);

                ProgressPct = 100;

                AppendLog(Loc.S.LogJobDone(plan.TargetOutputPath), LogLevel.Ok);
                await SoundPlayer.PlayCompleteAsync();
                Path.GetDirectoryName(plan.TargetOutputPath)?.OpenFolder();
            }
            catch (OperationCanceledException)
            {
                AppendLog(Loc.S.LogJobCancelled, LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
                MainViewModel.SetTaskbarProgress(0);
                TryDeleteFileAndFolder(plan.TargetOutputPath, plan.GameDirectory);
            }
            catch (Exception ex)
            {
                AppendLog(Loc.S.LogErrorWithTitle(gameTitle, ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                TryDeleteFileAndFolder(plan.TargetOutputPath, plan.GameDirectory);
            }
            finally
            {
                CleanupTask();
            }
        }
    }

    private void TryDeleteFileAndFolder(string? filePath, string? folderPath)
    {
        try
        {
            PackingJobRunner.CleanupFailedOutput(filePath, folderPath);
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogCleanupException(ex.Message), LogLevel.Error);
            _= SoundPlayer.PlayErrorAsync();
        }
    }
}