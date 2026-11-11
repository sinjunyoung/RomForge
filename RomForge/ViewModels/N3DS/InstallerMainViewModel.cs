using Common.Models;
using Common.Localization;
using N3DS.Core.Crypto;
using N3DS.Core.Enums;
using N3DS.Core.FileSystem;
using N3DS.Core.Models;
using N3DS.Core.Services;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Models.N3DS;
using RomForge.Core.Services;
using RomForge.Core.Services.N3DS;
using RomForge.Core.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;

namespace RomForge.ViewModels.N3DS;

public class InstallerMainViewModel : ProgressToolTabViewModel
{
    private bool _isLoading;
    private KeyStore? _keyStore;
    private SdCrypto? _sdCrypto;
    private SdTitleScanner? _scanner;
    private CancellationTokenSource? _extractCts;

    private IEnumerable<ToolTabViewModel> AllTabs => [InstalledTitles, Install];

    private static readonly string[] MovableSearchPaths = [ "movable.sed", @"gm9\out\movable.sed", ];

    public InstalledTitlesMainViewModel InstalledTitles { get; }

    public InstallMainViewModel Install { get; } = new();

    public int SubTabIndex
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(SelectedViewModel));
            OnPropertyChanged(nameof(SelectedTool));
            OnPropertyChanged(nameof(HasHelpKey));
            OnPropertyChanged(nameof(HelpPageKey));
            SyncLogEntries();
        }
    }

    public override bool HasHelpKey => SelectedViewModel?.HasHelpKey ?? false;

    public ToolTabViewModel? SelectedViewModel
    {
        get => (SubTabIndex >= 0 && SubTabIndex < Tools.Count) ? Tools[SubTabIndex] : null;

        set
        {
            if (value == null)
                return;

            var index = Tools.IndexOf(value);

            if (index != -1)
            {
                SubTabIndex = index;
                OnPropertyChanged(nameof(SelectedViewModel));
                OnPropertyChanged(nameof(HasHelpKey));
                OnPropertyChanged(nameof(HelpPageKey));
            }
        }
    }

    public ToolTabViewModel? SelectedTool => (SubTabIndex >= 0 && SubTabIndex < Tools.Count) ? Tools[SubTabIndex] : null;

    public override string? HelpPageKey => SelectedViewModel?.HelpPageKey;

    public string SdPath
    {
        get;
        set
        {
            SetProperty(ref field, value);
            InvalidateSession();
            OnPropertyChanged(nameof(CanLoad));
            OnPropertyChanged(nameof(CanInstall));
        }
    } = string.Empty;

    public string MovablePath
    {
        get;
        set
        {
            SetProperty(ref field, value);
            InvalidateSession();
            OnPropertyChanged(nameof(CanLoad));
            OnPropertyChanged(nameof(CanInstall));
        }
    } = string.Empty;

    public string StatusMessage { get; set { SetProperty(ref field, value); } } = Loc.S.StatusSelectSdAndMovable;

    public bool IsPathValid => !string.IsNullOrEmpty(SdPath) && !string.IsNullOrEmpty(MovablePath);

    public bool CanLoad => IsPathValid && !_isLoading && InstalledTitles.IsUnlocked;

    public Visibility ProgressPanelVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CancelButtonVisibility => _extractCts != null ? Visibility.Visible : Visibility.Collapsed;

    public bool CanInstall => CanLoad && Install.IsUnlocked && Install.IsNotInstalling;

    public event EventHandler? RunNavigateCerts;

    public InstallerMainViewModel()
    {
        InstalledTitles = new InstalledTitlesMainViewModel(msg => StatusMessage = msg);

        Tools.Add(InstalledTitles);
        Tools.Add(Install);

        InitializeMultiTools();

        InstalledTitles.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(InstalledTitles.IsUnlocked))
                OnPropertyChanged(nameof(CanLoad));
        };

        Install.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Install.IsInstalling) or nameof(Install.IsLocked))
                OnPropertyChanged(nameof(CanInstall));
        };

        foreach (var tab in AllTabs)
            tab.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IsLocked))
                    OnPropertyChanged(nameof(IsIdle));
            };
    }

    private void InvalidateSession()
    {
        _keyStore = null;
        _sdCrypto = null;
        _scanner = null;
    }

    private void EnsureInitialized()
    {
        if (_keyStore != null)
            return;

        _keyStore = new KeyStore();

        _keyStore.LoadMovable(MovablePath);

        _sdCrypto = new SdCrypto(_keyStore);
        _scanner = new SdTitleScanner(SdPath, _keyStore, _sdCrypto);
    }

    private async Task RunOperationAsync(CancellationTokenSource cts, string startMsg, Func<CancellationToken, Task> operation, string successMsg)
    {
        SetLoading(true);
        StatusMessage = startMsg;
        AppendLog(startMsg, LogLevel.Info);

        try
        {
            await operation(cts.Token);

            StatusMessage = successMsg;

            AppendLog(successMsg, LogLevel.Info);
            await SoundPlayer.PlayCompleteAsync();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.S.MsgCancelled;

            MainViewModel.SetTaskbarProgress(0);
            AppendLog(Loc.S.LogExtractCancelled, LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.S.LogError(ex.Message);

            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
            AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            throw;
        }
        finally
        {
            cts.Dispose();
            SetLoading(false);
        }
    }

    public async Task LoadTitlesAsync(Action<int, int>? onProgress = null, CancellationToken ct = default)
    {
        SetLoading(true);

        StatusMessage = Loc.S.StatusInitializing;

        ClearLog();
        AppendLog(Loc.S.LogScanStarting, LogLevel.Highlight);
        AppendLog(Loc.S.LogSdAndMovablePaths(SdPath, MovablePath), LogLevel.Info);

        using (InstalledTitles.BeginWork())
        {
            try
            {
                EnsureInitialized();

                await InstalledTitles.LoadAsync(_keyStore!, _sdCrypto!, _scanner!,
                    (current, total) =>
                    {
                        ProgressPct = (int)((double)current / total * 100);

                        MainViewModel.SetTaskbarProgress(ProgressPct);

                        ProgressLabel = Loc.S.ProgressLoadingCount(current, total);

                        onProgress?.Invoke(current, total);
                    }, ct);

                AppendLog(Loc.S.LogTitleListLoaded, LogLevel.Ok);
            }
            catch (Exception ex)
            {
                StatusMessage = Loc.S.LogError(ex.Message);

                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                throw;
            }
        }

        SetLoading(false);
        OnPropertyChanged(nameof(CanLoad));
    }

    public async Task RecoverTitleDbAsync(CancellationToken ct = default)
    {
        if (!MessageBoxHelper.ShowQuestion(Loc.S.MsgConfirmRebuildTitleDb))
            return;

        SetLoading(true);

        StatusMessage = Loc.S.StatusInitializing;

        ClearLog();
        AppendLog(Loc.S.LogRecoveryStarting, LogLevel.Highlight);
        AppendLog(Loc.S.LogSdAndMovablePaths(SdPath, MovablePath), LogLevel.Info);

        using (InstalledTitles.BeginWork())
        using (Install.BeginWork())
        {
            try
            {
                EnsureInitialized();

                var rebuilder = new TitleDbRebuilder(_keyStore!, _sdCrypto!, _scanner!);

                rebuilder.OnLog += msg => AppendLog(msg, LogLevel.Info);

                var scanProgress = new Progress<(int current, int total)>(p =>
                {
                    ProgressPct = p.total == 0 ? 0 : (int)((double)p.current / p.total * 100);
                    ProgressLabel = Loc.S.ProgressScanningCount(p.current, p.total);
                    MainViewModel.SetTaskbarProgress(ProgressPct);
                });

                void Report(int current, int total) => ((IProgress<(int, int)>)scanProgress).Report((current, total));

                StatusMessage = Loc.S.StatusRecoveringTitleDb;

                var (rebuilt, skipped) = await rebuilder.RebuildAsync(Report, ct);

                StatusMessage = Loc.S.StatusRecoveryDone(rebuilt);
                
                AppendLog(Loc.S.LogRecoveryRegistered(rebuilt), LogLevel.Ok);
                await SoundPlayer.PlayCompleteAsync();

                if (skipped > 0)
                    AppendLog(Loc.S.LogRecoverySkipped(skipped), LogLevel.Error);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = Loc.S.MsgCancelled;
                MainViewModel.SetTaskbarProgress(0);
                AppendLog(Loc.S.LogRecoveryCancelled, LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = Loc.S.LogError(ex.Message);                
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
            }
        }

        SetLoading(false);
        OnPropertyChanged(nameof(CanLoad));
    }

    public async Task ExtractTitleAsync(TitleViewModel selected, string outputPath, bool asCci)
    {
        using (InstalledTitles.BeginWork())
        {
            EnsureInitialized();

            _extractCts = new CancellationTokenSource();

            OnPropertyChanged(nameof(CancelButtonVisibility));

            var extractor = new TitleExtractor(_keyStore!, _sdCrypto!, _scanner!);
            var extractProgress = new Progress<ProgressInfo>(p =>
            {
                ProgressPct = p.Percent;
                MainViewModel.SetTaskbarProgress(ProgressPct);
                ProgressLabel = $"{p.Percent}%";
                ProgressTime = p.TimeInfo;
                ProgressSpeed = p.Speed;
                StatusMessage = Loc.S.StatusExtractingTitle(selected.ShortDescription, p.Label);
            });

            try
            {
                AppendLog(Loc.S.LogExtractReady(selected.ShortDescription, selected.TitleId), LogLevel.Info);
                AppendLog(Loc.S.LogOutputPathAndFormat(outputPath, asCci ? "CCI" : "CIA"), LogLevel.Info);
                await RunOperationAsync(_extractCts, Loc.S.LogExtractStarted(selected.ShortDescription),
                    async ct =>
                    {
                        if (asCci)
                            await extractor.ExtractToCciAsync(selected.Title, outputPath, extractProgress, ct);
                        else
                            await extractor.ExtractToCiaAsync(selected.Title, outputPath, extractProgress, ct);
                    },
                    Loc.S.LogExtractDone(selected.ShortDescription));
                await SoundPlayer.PlayCompleteAsync();
            }
            catch (CertsBinNotFoundException)
            {
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                RunNavigateCerts?.Invoke(this, EventArgs.Empty);
                await SoundPlayer.PlayErrorAsync();
            }
            finally
            {
                _extractCts = null;

                OnPropertyChanged(nameof(CancelButtonVisibility));
            }
        }
    }

    public async Task InstallAsync(TitleViewModel selected, CancellationToken ct)
    {
        if (selected.Progress == 100)
            return;

        using (Install.BeginWork())
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(selected.TitleId) || selected.TitleId.Length < 16)
            {
                AppendLog(Loc.S.LogInvalidTitleIdConfigured, LogLevel.Error);
                throw new InvalidOperationException(Loc.S.ErrInvalidTitleId);
            }

            string tidHigh = selected.TitleId[..8];
            string tidLow = selected.TitleId[8..];
            string titleRoot = Path.Combine(_scanner!.Id1Path, "title", tidHigh, tidLow);

            if (Directory.Exists(titleRoot))
            {
                AppendLog(Loc.S.LogTitleAlreadyExists(titleRoot), LogLevel.Info);
                var result = MessageBoxHelper.ShowQuestion(Loc.S.MsgConfirmReinstall(selected.ShortDescription));

                if (!result)
                {
                    AppendLog(Loc.S.LogReinstallDeclined, LogLevel.Info);
                    return;
                }

                AppendLog(Loc.S.LogDeletingInstalledTitleFolder, LogLevel.Info);
                Directory.Delete(titleRoot, true);
            }

            SetLoading(true);
            StatusMessage = Loc.S.StatusInstallStarted(selected.ShortDescription);
            AppendLog(Loc.S.StatusInstallStarted(selected.FilePath), LogLevel.Highlight);

            try
            {
                var installer = new TitleInstaller(_keyStore!, _sdCrypto!, _scanner!);

                installer.OnLog += msg => AppendLog($"{msg}", LogLevel.Info);

                var installProgress = new Progress<ProgressInfo>(p =>
                {
                    ProgressPct = p.Percent;
                    MainViewModel.SetTaskbarProgress(ProgressPct);
                    selected.Progress = p.Percent;
                    ProgressLabel = $"{p.Percent}%";
                    ProgressTime = p.TimeInfo;
                    ProgressSpeed = p.Speed;
                });

                string ext = Path.GetExtension(selected.FilePath).ToLowerInvariant();
                switch (ext)
                {
                    case ".cia":
                        {
                            var reader = new CiaReader(_keyStore!);
                            await using var cia = await reader.OpenAsync(selected.FilePath, ct: ct);
                            await installer.InstallAsync(cia, installProgress, ct);
                            break;
                        }
                    case ".cci":
                    case ".3ds":
                    case ".zcci":
                        {
                            await using var cci = await CciSource.OpenAsync(selected.FilePath, _keyStore!, ct: ct);
                            await installer.InstallAsync(cci, installProgress, ct);
                            break;
                        }
                    default:
                        AppendLog(Loc.S.ErrUnsupportedFormat(ext), LogLevel.Error);
                        throw new NotSupportedException(Loc.S.ErrUnsupportedFormat(ext));
                }

                StatusMessage = Loc.S.StatusInstallDone(selected.ShortDescription);                
                AppendLog(Loc.S.StatusInstallDone(selected.ShortDescription), LogLevel.Ok);
                await SoundPlayer.PlayCompleteAsync();
                AppendLog(Loc.S.LogHomebrewFinalizeHint, LogLevel.Highlight);

                if (selected.ForcedLanguage != LocaleLanguage.None)
                {
                    if (selected.LocaleRegionCode == LocaleRegion.None)
                    {
                        AppendLog(Loc.S.LogLocaleFixSkippedNoRegion, LogLevel.Error);
                        await SoundPlayer.PlayErrorAsync();
                    }
                    else
                    {
                        await LocaleTxtWriter.WriteAsync(_scanner!.SdRoot, selected.TitleId, selected.ForcedLanguage, ct);
                        AppendLog(Loc.S.LogLocaleFixed(LocaleTxtWriter.LanguageToRegion[selected.ForcedLanguage], selected.ForcedLanguage), LogLevel.Ok);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = Loc.S.MsgCancelled;

                AppendLog(Loc.S.LogInstallCancelled, LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
                MainViewModel.SetTaskbarProgress(0);
                if (Directory.Exists(titleRoot))
                    try
                    { 
                        Directory.Delete(titleRoot, true);
                    }
                    catch { }
                throw;
            }
            catch (Exception ex)
            {
                StatusMessage = Loc.S.LogError(ex.Message);
                
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                throw;
            }
            finally
            {
                SetLoading(false);
            }
        }
    }

    public void CancelExtract() => _extractCts?.Cancel();

    public async Task CheckAndSetMovablePathAsync(string driveLetter)
    {
        var found = await Task.Run(() => MovableSearchPaths
        .Select(p => Path.Combine(driveLetter, p))
        .FirstOrDefault(File.Exists));

        MovablePath = found ?? string.Empty;
    }

    private void SetLoading(bool loading)
    {
        _isLoading = loading;

        OnPropertyChanged(nameof(CanLoad));
        OnPropertyChanged(nameof(ProgressPanelVisibility));

        if (!loading)
            CleanupTask();
    }

    private void InitializeMultiTools()
    {
        foreach (var tool in AllTabs)
        {
            RegisterChild(tool);

            tool.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HasHelpKey) && SelectedViewModel == tool)
                    OnPropertyChanged(nameof(HasHelpKey));

                if (e.PropertyName == nameof(HelpPageKey) && SelectedViewModel == tool)
                    OnPropertyChanged(nameof(HelpPageKey));
            };

            var logProp = tool.GetType().GetProperty(nameof(LogEntries));

            if (logProp?.GetValue(tool) is ObservableCollection<LogEntry> childLogs)
                childLogs.CollectionChanged += (_, e) => LogEntries_CollectionChanged(e, tool);
        }

        SyncLogEntries();
    }

    private void LogEntries_CollectionChanged(NotifyCollectionChangedEventArgs e, ToolTabViewModel tool)
    {
        if (SelectedViewModel != tool)
            return;

        if (Application.Current?.Dispatcher != null)
            Application.Current.Dispatcher.Invoke(() => HandleCollectionChanged(e));
        else
            HandleCollectionChanged(e);
    }

    private void HandleCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                    foreach (LogEntry item in e.NewItems)
                        LogEntries.Add(item);
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                    foreach (LogEntry item in e.OldItems)
                        LogEntries.Remove(item);
                break;

            case NotifyCollectionChangedAction.Reset:
                LogEntries.Clear();
                break;
        }
    }

    private void SyncLogEntries()
    {
        if (Application.Current?.Dispatcher != null)
            Application.Current.Dispatcher.Invoke(DoSync);
        else
            DoSync();
    }

    private void DoSync()
    {
        LogEntries.Clear();

        var currentTool = SelectedViewModel;

        if (currentTool == null)
            return;

        var logProp = currentTool.GetType().GetProperty(nameof(LogEntries));

        if (logProp?.GetValue(currentTool) is ObservableCollection<LogEntry> childLogs)
        {
            foreach (var item in childLogs)
                LogEntries.Add(item);
        }
    }
}