using Common.Models;
using Common.Localization;
using Common.Services;
using RomForge.Core;
using RomForge.Core.Models.WiiU;
using RomForge.Core.Services;
using RomForge.Core.Services.WiiU;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WiiU.Core.Services;

namespace RomForge.ViewModels.WiiU;

public class RepackMainViewModel : ProgressToolTabViewModel
{
    private static string KeysPath => WiiUKeyLocator.KeysPath;

    private BuildMode? _currentMode;

    public RepackOutputFormat OutputFormat { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(IsWuaFormat)); OnPropertyChanged(nameof(IsWupFormat)); } } = RepackOutputFormat.Wua;

    public bool IsWuaFormat
    {
        get => OutputFormat == RepackOutputFormat.Wua;
        set
        {
            if (value)
                OutputFormat = RepackOutputFormat.Wua;
        }
    }

    public bool IsWupFormat
    {
        get => OutputFormat == RepackOutputFormat.Wup;
        set
        {
            if (value)
                OutputFormat = RepackOutputFormat.Wup;
        }
    }

    public ObservableCollection<TitleInputEntry> Entries { get; } = [];

    public TitleInputEntry? SelectedEntry { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(HasSelection)); } }

    public string OutputPath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(OutputHintVisibility)); AppConfig.Instance.OutputFolders.WiiURepackOutputPath = value; } } = string.Empty;

    public Visibility EntriesHintVisibility => Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public bool HasSelection => SelectedEntry != null;

    public bool KeysPathRequired => Entries.Any(e => !e.IsFolder && !string.Equals(Path.GetExtension(e.FilePath), ".wua", StringComparison.OrdinalIgnoreCase));

    public bool IsUnpackRunning => IsLocked && _currentMode == BuildMode.UnpackOnly;

    public bool IsRebuildRunning => IsLocked && _currentMode == BuildMode.RebuildOnly;

    public bool IsFullRunning => IsLocked && _currentMode == BuildMode.FullProcess;

    public bool UnpackEnabled => !IsLocked || _currentMode == BuildMode.UnpackOnly;

    public bool RebuildEnabled => !IsLocked || _currentMode == BuildMode.RebuildOnly;

    public bool StartEnabled => !IsLocked || _currentMode == BuildMode.FullProcess;

    public ICommand RemoveSelectedCommand { get; }

    public ICommand RemoveAllCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public RepackMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.WiiURepackOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.WiiURepackOutputPath;
        RemoveSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => HasSelection);
        RemoveAllCommand = new RelayCommand(_ => Entries.Clear(), _ => Entries.Count > 0);
        BrowseOutputCommand = new RelayCommand(async _ => await BrowseOutput());
        Entries.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(EntriesHintVisibility));
            OnPropertyChanged(nameof(KeysPathRequired));
        };
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsLocked))
                NotifyButtonStates();
        };

        _ = LoadExistingUnpackedAsync();
    }

    public async Task LoadExistingUnpackedAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputPath) || Entries.Any(e => !e.IsUnpacked))
            return;

        string outputPath = OutputPath;
        var scanned = await Task.Run(() => RepackService.ScanUnpacked(outputPath));

        if (Entries.Any(e => !e.IsUnpacked) || !string.Equals(outputPath, OutputPath, StringComparison.Ordinal))
            return;

        RemoveUnpackedEntries();

        foreach (var row in scanned)
            Entries.Add(row);

        EntryListOrganizer.Reorganize(Entries);

        if (scanned.Count > 0)
            AppendLog(Loc.S.LogUnpackFolderLoadedTitles(scanned.Count), LogLevel.Info);
    }

    private void RemoveUnpackedEntries()
    {
        var unpackedEntries = Entries.Where(e => e.IsUnpacked).ToList();

        if (unpackedEntries.Count == 0)
            return;

        foreach (var entry in unpackedEntries)
            Entries.Remove(entry);

        AppendLog(Loc.S.LogUnpackFolderItemRemoved, LogLevel.Info);
    }

    private bool IsDuplicate(string path) => Entries.Any(e => string.Equals(e.FilePath, path, StringComparison.OrdinalIgnoreCase));

    private void AssignByGuessedRole(TitleInputEntry row)
    {
        if (row.Role is TitleRole.Base or TitleRole.Update)
        {
            var existing = Entries.FirstOrDefault(e => e.Role == row.Role);

            if (existing is not null)
            {
                Entries.Remove(existing);
                AppendLog(Loc.S.LogReplacingExistingKind(row.Kind), LogLevel.Info);
            }
        }

        Entries.Add(row);
    }

    public async Task AddDroppedItemAsync(string path)
    {
        if (IsDuplicate(path))
            return;

        try
        {
            if (Directory.Exists(path))
            {
                if (!LooksLikeSupportedFolder(path))
                {
                    AppendLog(Loc.S.LogCannotAutoAddFolder(Path.GetFileName(path)), LogLevel.Error);
                    await SoundPlayer.PlayErrorAsync();
                    return;
                }

                var row = RepackService.PeekFolder(path);

                RemoveUnpackedEntries();
                AssignByGuessedRole(row);
                EntryListOrganizer.Reorganize(Entries);
                return;
            }

            string ext = Path.GetExtension(path);
            bool isWua = string.Equals(ext, ".wua", StringComparison.OrdinalIgnoreCase);
            bool isWudOrWux = string.Equals(ext, ".wud", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".wux", StringComparison.OrdinalIgnoreCase);

            if (!isWua && !isWudOrWux)
            {
                AppendLog(Loc.S.LogUnsupportedWiiUInputFormat(Path.GetFileName(path)), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                return;
            }

            if (!isWua && !File.Exists(KeysPath))
            {
                AppendLog(Loc.S.ErrWiiUKeysNotFound, LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                return;
            }

            var rows = await RepackService.PeekFileAsync(path, KeysPath, CancellationToken.None);

            RemoveUnpackedEntries();

            foreach (var row in rows)
                AssignByGuessedRole(row);

            EntryListOrganizer.Reorganize(Entries);

            AppendLog(Loc.S.LogTitlesAdded(Path.GetFileName(path), rows.Count), LogLevel.Info);
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogAddFailed(path, ex.Message), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
        }
    }

    public override string? HelpPageKey => "wiiu-repack";

    private static bool LooksLikeSupportedFolder(string path) => WupTitleSource.LooksLikeWupFolder(path) || (Directory.Exists(Path.Combine(path, "code")) && Directory.Exists(Path.Combine(path, "content")) && Directory.Exists(Path.Combine(path, "meta")));

    private void RemoveSelected()
    {
        if (SelectedEntry is not null)
            Entries.Remove(SelectedEntry);
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
        string unpackedRoot = Path.Combine(OutputPath, "unpacked");

        if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedRoot))
        {
            if (!MessageBoxHelper.ShowQuestion(Loc.S.MsgConfirmDiscardUnpacked))
                return;

            Directory.Delete(unpackedRoot, true);
        }

        if (mode == BuildMode.RebuildOnly)
        {
            var scanned = RepackService.ScanUnpacked(OutputPath);

            if (scanned.Count == 0)
            {
                AppendLog(Loc.S.ErrNoUnpackedData, LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                return;
            }

            foreach (var row in scanned)
            {
                var existing = Entries.FirstOrDefault(e => e.TitleIdHex == row.TitleIdHex && e.TitleVersion == row.TitleVersion);

                if (existing?.PatchPath is not null)
                    row.PatchPath = existing.PatchPath;
            }

            Entries.Clear();

            foreach (var row in scanned)
                Entries.Add(row);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var progress = BuildProgressReporter();
        bool isCompleted = false;

        try
        {
            Directory.CreateDirectory(OutputPath);
            var entriesSnapshot = Entries.ToList();

            switch (mode)
            {
                case BuildMode.UnpackOnly:
                    await RepackService.UnpackAsync(entriesSnapshot, KeysPath, OutputPath, progress, AppendLog, ct);
                    break;
                case BuildMode.RebuildOnly:
                case BuildMode.FullProcess:
                    await RepackService.RepackAsync(entriesSnapshot, KeysPath, OutputPath, OutputFormat, progress, AppendLog, ct);
                    break;
            }

            isCompleted = true;
            ProgressPercent = "100%";

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
            CleanupTask();

            if (!isCompleted && mode == BuildMode.UnpackOnly && Directory.Exists(unpackedRoot))
            {
                try
                {
                    Directory.Delete(unpackedRoot, true);
                }
                catch { }
            }
        }
    }

    private bool Validate(BuildMode mode, out string error)
    {
        error = string.Empty;

        if (mode != BuildMode.RebuildOnly && Entries.Count == 0)
        {
            error = Loc.S.ErrAddAtLeastOneTitle;
            return false;
        }

        if (mode == BuildMode.UnpackOnly && Entries.Any(e => e.IsUnpacked))
        {
            error = Loc.S.ErrUnpackedFolderEntryWiiU;
            return false;
        }

        if (mode != BuildMode.RebuildOnly && KeysPathRequired && !File.Exists(KeysPath))
        {
            error = Loc.S.ErrWiiUKeysNotFound;
            return false;
        }

        if (string.IsNullOrEmpty(OutputPath))
        {
            error = Loc.S.ErrSelectWorkFolder;
            return false;
        }

        if (mode == BuildMode.RebuildOnly)
        {
            string unpackedPath = Path.Combine(OutputPath, "unpacked");
            if (!Directory.Exists(unpackedPath))
            {
                error = Loc.S.ErrNoUnpackedData;
                return false;
            }
        }

        return true;
    }

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

    private async Task BrowseOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = Loc.S.DialogSelectWorkFolder };

        if (dlg.ShowDialog() != true)
            return;

        OutputPath = dlg.FolderName;

        await LoadExistingUnpackedAsync();
    }
}