using Common.Models;
using Common.Localization;
using Common.Services;
using RomForge.Core;
using RomForge.Core.Models.Wii;
using RomForge.Core.Services;
using RomForge.Core.Services.Wii;
using RomForge.Core.UI.Command;
using RomForge.Views;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WiiGC.Core.Enums;
using WiiGC.Core.Models.Wii;
using WiiGC.Core.Services.Wii;

namespace RomForge.ViewModels.Wii;

public class RepackMainViewModel : ProgressToolTabViewModel
{
    private static readonly string[] DiscExtensions = [".iso", ".wbfs", ".rvz", ".wia"];

    public static bool IsSupportedDisc(string path) => File.Exists(path) && DiscExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private const int RvzCompressionLevel = 5;
    private const int RvzChunkSize = 131072;

    private CancellationTokenSource _patchCts = new();
    private BuildMode? _currentMode;
    private bool _patchLoading;
    private RiivolutionWorkspace? _workspace;
    private RiivolutionPatchSet? _patch;
    private DiscInfo? _disc;
    private int _discVersion;
    private int _patchVersion;

    public OutputFormat OutputFormat
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(IsIsoFormat));
            OnPropertyChanged(nameof(IsWbfsFormat));
            OnPropertyChanged(nameof(IsRvzFormat));
        }
    } = OutputFormat.Iso;

    public bool IsIsoFormat
    {
        get => OutputFormat == OutputFormat.Iso;
        set
        {
            if (value)
                OutputFormat = OutputFormat.Iso;
        }
    }

    public bool IsWbfsFormat
    {
        get => OutputFormat == OutputFormat.Wbfs;
        set
        {
            if (value)
                OutputFormat = OutputFormat.Wbfs;
        }
    }

    public bool IsRvzFormat
    {
        get => OutputFormat == OutputFormat.Rvz;
        set
        {
            if (value)
                OutputFormat = OutputFormat.Rvz;
        }
    }

    public string InputPath
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(InputHintVisibility));
            _ = RefreshDiscAsync();
        }
    } = string.Empty;

    public string PatchPath
    {
        get;
        set
        {
            if (field != value)
            {
                SetProperty(ref field, value);
                OnPropertyChanged(nameof(PatchHintVisibility));
                _ = RefreshPatchAsync();
            }
        }
    } = string.Empty;

    public string OutputPath
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(OutputHintVisibility));

            AppConfig.Instance.OutputFolders.WiiOutputPath = value;

            if (string.IsNullOrWhiteSpace(InputPath))
                _ = RefreshUnpackedFolderAsync();
        }
    } = string.Empty;

    public GameDisplay? GameInfo { get; private set { SetProperty(ref field, value); OnPropertyChanged(nameof(GameInfoVisibility)); } }

    public ImageSource? GameIcon { get; private set { SetProperty(ref field, value); } }

    public PatchDisplay? PatchInfo { get; private set { SetProperty(ref field, value); OnPropertyChanged(nameof(PatchInfoVisibility)); } }

    public Visibility InputHintVisibility => string.IsNullOrEmpty(InputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PatchHintVisibility => string.IsNullOrEmpty(PatchPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GameInfoVisibility => GameInfo != null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PatchInfoVisibility => PatchInfo != null ? Visibility.Visible : Visibility.Collapsed;

    public bool IsUnpackRunning => IsLocked && _currentMode == BuildMode.UnpackOnly;

    public bool IsRebuildRunning => IsLocked && _currentMode == BuildMode.RebuildOnly;

    public bool IsFullRunning => IsLocked && (_currentMode == BuildMode.FullProcess || _patchLoading);

    public bool UnpackEnabled => !IsLocked || _currentMode == BuildMode.UnpackOnly;

    public bool RebuildEnabled => !IsLocked || _currentMode == BuildMode.RebuildOnly;

    public bool StartEnabled => !IsLocked || _currentMode == BuildMode.FullProcess || _patchLoading;

    public ICommand BrowseInputCommand { get; }

    public ICommand BrowsePatchFolderCommand { get; }

    public ICommand BrowsePatchFileCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public RepackMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.WiiOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.WiiOutputPath;
        BrowseInputCommand = new RelayCommand(_ => BrowseInput());
        BrowsePatchFolderCommand = new RelayCommand(_ => BrowsePatchFolder());
        BrowsePatchFileCommand = new RelayCommand(_ => BrowsePatchFile());
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());

        AppDomain.CurrentDomain.ProcessExit += (_, _) => _workspace?.Dispose();

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsLocked))
                NotifyButtonStates();
        };

        if (string.IsNullOrWhiteSpace(InputPath))
            _ = RefreshUnpackedFolderAsync();
    }

    public override void Cancel()
    {
        base.Cancel();
        _patchCts.Cancel();
    }

    public async Task StartAsync(BuildMode mode)
    {
        if (!Validate(mode, out string error))
        {
            AppendLog(error, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        _currentMode = mode;
        NotifyButtonStates();

        using (BeginWork())
        {
            try
            {
                CancellationTokenSource?.Dispose();

                CancellationTokenSource = new CancellationTokenSource();

                await ExecuteAsync(mode, CancellationTokenSource.Token);
            }
            finally
            {   
                _currentMode = null;

                CleanupTask();
                NotifyButtonStates();
            }
        }
    }

    private async Task ExecuteAsync(BuildMode mode, CancellationToken ct)
    {
        string unpackedPath = Path.Combine(OutputPath, "unpacked");

        if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedPath))
        {
            if (!MessageBoxHelper.ShowQuestion(Loc.S.MsgConfirmDiscardUnpacked))
                return;

            Directory.Delete(unpackedPath, true);
        }

        var sw = Stopwatch.StartNew();
        var tempOutputs = new List<string>();
        bool isCompleted = false;

        try
        {
            Directory.CreateDirectory(OutputPath);

            if (mode == BuildMode.UnpackOnly)
            {
                string source = InputPath;

                void progress(double value) => SetProgress(value, Loc.S.ProgressUnpackingFile(Path.GetFileName(source)), sw);

                SetProgress(0, Loc.S.ProgressUnpackingFile(Path.GetFileName(source)), sw);
                AppendLog(Loc.S.LogUnpackStartingTo(unpackedPath), LogLevel.Highlight);

                int count = await Task.Run(() => IsoUnpacker.Unpack(source, unpackedPath, progress, ct), ct);

                AppendLog(Loc.S.LogFilesUnpackedCount(count.ToString("N0")), LogLevel.Ok);
            }
            else
                await ProduceAsync(mode, unpackedPath, sw, tempOutputs, ct);

            isCompleted = true;
            ProgressPercent = "100%";
            MainViewModel.SetTaskbarProgress(100);

            AppendLog(Loc.S.LogDoneTotalTime(sw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);
            await SoundPlayer.PlayCompleteAsync();
            OutputPath.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            AppendLog(Loc.S.LogJobCancelled, LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            MainViewModel.SetTaskbarProgress(0);
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
        }
        finally
        {
            if (!isCompleted)
            {
                foreach (string path in tempOutputs.Distinct())
                {
                    if (File.Exists(path))
                        try 
                        {
                            File.Delete(path); 
                        } 
                        catch { }
                }

                if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedPath))
                    try 
                    { 
                        Directory.Delete(unpackedPath, true);
                    } 
                    catch { }
            }
        }
    }

    private async Task ProduceAsync(BuildMode mode, string unpackedPath, Stopwatch sw, List<string> tempOutputs, CancellationToken ct)
    {
        bool fromFolder = mode == BuildMode.RebuildOnly;
        string title = string.Empty;
        string gameId = string.Empty;

        if (!string.IsNullOrWhiteSpace(InputPath))
        {
            title = _disc?.Title?.Trim() ?? string.Empty;
            gameId = _disc?.GameId?.Trim() ?? string.Empty;
        }
        else
        {
            var folderInfo = FolderInfo.TryRead(unpackedPath);

            title = folderInfo?.Title?.Trim() ?? string.Empty;
            gameId = folderInfo?.GameId?.Trim() ?? string.Empty;
        }

        string fallbackName = !string.IsNullOrWhiteSpace(InputPath) ? Path.GetFileNameWithoutExtension(InputPath) : "WiiGame";
        string name = (!string.IsNullOrEmpty(title), !string.IsNullOrEmpty(gameId)) switch
        {
            (true, true) => $"{title} [{gameId}]",
            (true, false) => title,
            (false, true) => gameId,
            _ => fallbackName
        };

        string baseName = Path.Combine(OutputPath, name + "_Repack");
        string finalPath = Utils.GetUniqueFilePath(baseName + GetExtension(OutputFormat));
        string target = Path.GetFileName(finalPath);
        var format = OutputFormat;
        var overlay = _patch?.Replacements;
        string prepareLabel = overlay != null ? Loc.S.ProgressApplyingPatchTo(target) : Loc.S.ProgressHashingPartitions(target);
        string convertLabel = format switch
        {
            OutputFormat.Wbfs => Loc.S.ProgressCompressingWbfs(target),
            OutputFormat.Rvz => Loc.S.ProgressCompressingRvz(target),
            _ => Loc.S.ProgressCreatingIso(target)
        };
        PatchResult result;

        void prepareProgress(double value) => SetProgress(value, prepareLabel, sw);
        void convertProgress(double value) => SetProgress(value, convertLabel, sw);

        tempOutputs.Add(finalPath);
        SetProgress(0, prepareLabel, sw);
        
        if (fromFolder)
        {
            AppendLog(Loc.S.LogRepackStartTarget(target), LogLevel.Highlight);

            result = await Task.Run(() =>
            {
                return format switch
                {
                    OutputFormat.Wbfs => IsoStreamConverter.RepackFolderToWbfs(unpackedPath, finalPath, overlay, prepareProgress, convertProgress, LogPatchEntry, ct),
                    OutputFormat.Rvz => IsoStreamConverter.RepackFolderToRvz(unpackedPath, finalPath, overlay, RvzCompressionLevel, RvzChunkSize, prepareProgress, convertProgress, LogPatchEntry, ct),
                    _ => IsoStreamConverter.RepackFolderToIso(unpackedPath, finalPath, overlay, prepareProgress, convertProgress, LogPatchEntry, ct),
                };
            }, ct);
        }
        else
        {
            AppendLog(Loc.S.LogStreamingRepackStartTarget(target), LogLevel.Highlight);

            string source = InputPath;
            IReadOnlyDictionary<string, string> replacements = overlay ?? new Dictionary<string, string>();

            if (format == OutputFormat.Iso)
            {
                void rebuildProgress(double value) => SetProgress(value, Loc.S.ProgressRepackingFile(Path.GetFileName(source)), sw);

                result = await Task.Run(() => IsoRebuilder.RebuildWithReplacements(source, finalPath, replacements, rebuildProgress, LogPatchEntry, ct), ct);
            }
            else
            {
                result = await Task.Run(() => format == OutputFormat.Wbfs
                    ? IsoStreamConverter.RebuildToWbfs(source, finalPath, replacements, prepareProgress, convertProgress, LogPatchEntry, ct)
                    : IsoStreamConverter.RebuildToRvz(source, finalPath, replacements, RvzCompressionLevel, RvzChunkSize, prepareProgress, convertProgress, LogPatchEntry, ct), ct);
            }
        }

        LogPatchResult(result);

        if (_patch != null)
        {
            foreach (string warning in _patch.Warnings)
                AppendLog(Loc.S.LabelWarningValue(warning), LogLevel.Error);
        }
    }

    private void LogPatchEntry(PatchEntry entry)
    {
        if (entry.Applied)
            AppendLog(Loc.S.LogReplaced(entry.Path), LogLevel.Info);
        else
            AppendLog(Loc.S.LogNotInDisc(entry.Path), LogLevel.Error);
    }

    private void LogPatchResult(PatchResult result)
    {
        if (result.Total == 0)
            return;

        int failed = result.Total - result.Applied;

        AppendLog(Loc.S.LogPatchResultSummary(result.Total.ToString("N0"), result.Applied.ToString("N0"), failed.ToString("N0")), failed == 0 ? LogLevel.Ok : LogLevel.Highlight);
    }

    private async Task RefreshDiscAsync()
    {
        int version = ++_discVersion;
        string path = InputPath;
        DiscInfo? info = null;

        if (File.Exists(path))
        {
            try
            {
                info = await Task.Run(() => DiscInfoReader.Read(path));
            }
            catch (Exception ex)
            {
                if (version == _discVersion)
                    AppendLog(Loc.S.LogDiscInfoReadFailed(ex.Message), LogLevel.Error);
            }
        }

        if (version != _discVersion)
            return;

        _disc = info;
        GameInfo = info == null ? null : GameDisplay.From(info);
        GameIcon = null;

        RebuildPatchDisplay();

        if (info != null)
            _ = LoadIconAsync(info.GameId, version);
    }

    private async Task LoadIconAsync(string gameId, int version)
    {
        byte[]? bytes;

        try
        {
            bytes = await CoverArtFetcher.TryGetDiscPngAsync(gameId);
        }
        catch
        {
            return;
        }

        if (bytes == null || version != _discVersion)
            return;

        var image = CreateImage(bytes);

        if (image != null)
            GameIcon = image;
    }

    private static BitmapImage? CreateImage(byte[] bytes)
    {
        try
        {
            return bytes.ToBitmapImage();
        }
        catch
        {
            return null;
        }
    }

    private async Task RefreshPatchAsync()
    {
        int version = ++_patchVersion;

        _patchCts.Cancel();
        ReleaseWorkspace();

        _patch = null;
        PatchInfo = null;

        string path = PatchPath.Trim();

        if (path.Length == 0)
            return;

        if (!RiivolutionWorkspace.IsSupported(path))
        {
            PatchInfo = PatchDisplay.Failed(Loc.S.MsgPatchPathMustBeFolderZip7zXml);

            AppendLog(Loc.S.LogUnsupportedPatchFormatNamed(Path.GetFileName(path)), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();

            return;
        }

        _patchCts = new CancellationTokenSource();

        var ct = _patchCts.Token;
        var sw = Stopwatch.StartNew();

        _patchLoading = true;

        using (BeginWork())
        {
            try
            {
                var workspace = await OpenWorkspaceAsync(path, sw, ct);

                if (workspace == null)
                {
                    if (version == _patchVersion)
                        PatchInfo = PatchDisplay.Failed(Loc.S.MsgArchiveOpenFailed);

                    return;
                }

                if (version != _patchVersion)
                {
                    workspace.Dispose();
                    return;
                }

                _workspace = workspace;
                ProgressLabel = Loc.S.ProgressAnalyzingPatchInfo;

                var patch = await Task.Run(() => RiivolutionParser.Parse(workspace.RootPath), ct);

                if (version != _patchVersion)
                    return;

                _patch = patch;

                RebuildPatchDisplay();
                AppendLog(Loc.S.LogPatchInfoReadWii(patch.Replacements.Count, patch.Warnings.Count), LogLevel.Info);
            }
            catch (OperationCanceledException)
            {
                if (version == _patchVersion)
                {
                    ReleaseWorkspace();

                    PatchInfo = PatchDisplay.Failed(Loc.S.MsgCancelled);

                    AppendLog(Loc.S.LogPatchReadCancelled, LogLevel.Error);
                    await SoundPlayer.PlayCancelAsync();
                }
            }
            catch (Exception ex)
            {
                if (version == _patchVersion)
                {
                    ReleaseWorkspace();

                    PatchInfo = PatchDisplay.Failed(ex.Message);

                    MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                    AppendLog(Loc.S.LogPatchReadFailed(ex.Message), LogLevel.Error);
                    await SoundPlayer.PlayErrorAsync();
                }
            }
            finally
            {
                _patchLoading = false;

                CleanupTask();
                NotifyButtonStates();
            }
        }
    }

    private async Task<RiivolutionWorkspace?> OpenWorkspaceAsync(string path, Stopwatch sw, CancellationToken ct)
    {
        if (!RiivolutionWorkspace.IsArchive(path))
            return RiivolutionWorkspace.OpenLocal(path);

        var opened = await PasswordPromptWindow.OpenWithPasswordPromptAsync(path, Application.Current.MainWindow);

        if (opened == null)
        {
            AppendLog(Loc.S.LogPasswordInputCancelled, LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            return null;
        }

        SetProgress(0, Loc.S.ProgressExtractingPatch, sw);

        return await RiivolutionWorkspace.ExtractAsync(path, opened.Value.Archive, value => SetProgress(value, Loc.S.ProgressExtractingPatch, sw), ct);
    }

    private void RebuildPatchDisplay()
    {
        if (_workspace == null || _patch == null)
            return;

        PatchInfo = PatchDisplay.From(_workspace.Kind, _patch, _disc?.GameId);
    }

    private void ReleaseWorkspace()
    {
        var old = _workspace;

        _workspace = null;

        if (old != null)
            _ = Task.Run(old.Dispose);
    }

    private void SetProgress(double value, string label, Stopwatch sw)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            int pct = (int)(value * 100);

            ProgressPct = pct;
            ProgressLabel = label;
            ProgressPercent = $"{pct}%";
            ProgressTime = Loc.S.ProgressElapsed(sw.Elapsed.ToString(@"mm\:ss"));
        });
    }

    private bool Validate(BuildMode mode, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            error = Loc.S.ErrSelectWorkFolder;
            return false;
        }

        string? gameId = _disc?.GameId;

        if (mode == BuildMode.RebuildOnly)
        {
            string unpackedPath = Path.Combine(OutputPath, "unpacked");

            if (!Directory.Exists(unpackedPath))
            {
                error = Loc.S.ErrNoUnpackedData;
                return false;
            }

            var folderInfo = FolderInfo.TryRead(unpackedPath);

            if (folderInfo == null)
            {
                error = Loc.S.ErrUnpackDataOldOrCorrupt;
                return false;
            }

            gameId = folderInfo.GameId;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(InputPath) || !File.Exists(InputPath))
            {
                error = Loc.S.ErrSelectSourceDiscFile;
                return false;
            }

            if (_disc == null)
            {
                error = Loc.S.ErrWiiDiscInfoUnreadable;
                return false;
            }

            if (mode == BuildMode.UnpackOnly)
                return true;
        }

        bool hasPatchPath = !string.IsNullOrWhiteSpace(PatchPath);

        if (hasPatchPath)
        {
            if (_workspace == null || _patch == null)
            {
                error = Loc.S.ErrPatchInfoUnreadable;
                return false;
            }

            if (gameId != null && !_patch.MatchesDisc(gameId))
            {
                error = Loc.S.ErrPatchGameIdMismatch(string.Join(", ", _patch.GameIds), gameId);
                return false;
            }
        }

        return true;
    }

    private async Task RefreshUnpackedFolderAsync()
    {
        int version = ++_discVersion;
        string unpackedPath = Path.Combine(OutputPath, "unpacked");

        if (!Directory.Exists(unpackedPath))
        {
            if (string.IsNullOrWhiteSpace(InputPath))
            {
                _disc = null;
                GameInfo = null;
                GameIcon = null;

                RebuildPatchDisplay();
            }

            return;
        }

        FolderInfo? folderInfo = null;

        try
        {
            folderInfo = await Task.Run(() => FolderInfo.TryRead(unpackedPath));
        }
        catch (Exception ex)
        {
            if (version == _discVersion)
            {
                AppendLog(Loc.S.LogUnpackFolderInfoReadFailed(ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
            }
        }

        if (version != _discVersion || folderInfo == null)
            return;

        _disc = new DiscInfo(folderInfo.GameId, folderInfo.Title, 0, folderInfo.Version, DiscContainerFormat.PlainDisc, 0, 0);
        GameInfo = GameDisplay.From(_disc);
        GameIcon = null;

        RebuildPatchDisplay();

        if (!string.IsNullOrEmpty(folderInfo.GameId))
            _ = LoadIconAsync(folderInfo.GameId, version);
    }

    private static string GetExtension(OutputFormat format) => format switch
    {
        OutputFormat.Wbfs => ".wbfs",
        OutputFormat.Rvz => ".rvz",
        _ => ".iso"
    };

    private void NotifyButtonStates()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(IsUnpackRunning));
            OnPropertyChanged(nameof(IsRebuildRunning));
            OnPropertyChanged(nameof(IsFullRunning));
            OnPropertyChanged(nameof(UnpackEnabled));
            OnPropertyChanged(nameof(RebuildEnabled));
            OnPropertyChanged(nameof(StartEnabled));
        });
    }

    private void BrowseInput()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.S.DialogSelectSourceWiiDisc,
            Filter = $"{Loc.S.LabelWiiDisc}|*.iso;*.wbfs;*.rvz;*.wia"
        };

        if (dlg.ShowDialog() == true)
            InputPath = dlg.FileName;
    }

    private void BrowsePatchFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = Loc.S.DialogSelectRiivolutionPatchFolder };

        if (dlg.ShowDialog() == true)
            PatchPath = dlg.FolderName;
    }

    private void BrowsePatchFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.S.DialogSelectRiivolutionPatch,
            Filter = $"{Loc.S.FilterRiivolutionPatch}|*.zip;*.7z;*.xml"
        };

        if (dlg.ShowDialog() == true)
            PatchPath = dlg.FileName;
    }

    private void BrowseOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = Loc.S.DialogSelectWorkFolder };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.FolderName;
    }
}