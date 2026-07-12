using Common;
using Common.WPF.ViewModels;
using NSW.Core.Enums;
using NSW.WPF.Services;
using NSW.WPF.UI;
using RomForge.Core.Models;
using RomForge.Core.Models.WiiU;
using RomForge.Core.Services.WiiU;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.WiiU;

public class RepackMainViewModel : ToolTabViewModel
{
    private CancellationTokenSource _cts = new();
    private BuildMode? _currentMode;
    private TitleInputEntry? _selectedEntry;
    private string _outputPath = string.Empty;
    private int _progressPct;
    private string _progressLabel = "대기 중...";
    private string _progressPercent = string.Empty;
    private string _progressTime = "00:00 경과";
    private string _progressSpeed = string.Empty;

    private static string KeysPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys.txt");

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ObservableCollection<TitleInputEntry> Entries { get; } = [];

    public TitleInputEntry? SelectedEntry
    {
        get => _selectedEntry;
        set { _selectedEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
    }

    public string OutputPath
    {
        get => _outputPath;
        set { _outputPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(OutputHintVisibility)); }
    }

    public int ProgressPct
    {
        get => _progressPct;
        set { _progressPct = value; OnPropertyChanged(); }
    }

    public string ProgressLabel
    {
        get => _progressLabel;
        set { _progressLabel = value; OnPropertyChanged(); }
    }

    public string ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); }
    }

    public string ProgressTime
    {
        get => _progressTime;
        set { _progressTime = value; OnPropertyChanged(); }
    }

    public string ProgressSpeed
    {
        get => _progressSpeed;
        set { _progressSpeed = value; OnPropertyChanged(); }
    }

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

    public ICommand BrowseAddFileCommand { get; }

    public ICommand BrowseAddFolderCommand { get; }

    public ICommand RemoveSelectedCommand { get; }

    public ICommand RemoveAllCommand { get; }

    public ICommand BrowsePatchForSelectedCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public RepackMainViewModel()
    {
        OutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

        BrowseAddFileCommand = new RelayCommand(async _ => await BrowseAddFile());
        BrowseAddFolderCommand = new RelayCommand(async _ => await BrowseAddFolder());
        RemoveSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => HasSelection);
        RemoveAllCommand = new RelayCommand(_ => RemoveAll(), _ => Entries.Count > 0);
        BrowsePatchForSelectedCommand = new RelayCommand(async _ => await BrowsePatchForSelected(), _ => HasSelection);
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
    }

    public async Task AddFileAsync(string path)
    {
        if (IsDuplicate(path))
            return;

        try
        {
            bool isWua = string.Equals(Path.GetExtension(path), ".wua", StringComparison.OrdinalIgnoreCase);

            if (!isWua && !File.Exists(KeysPath))
            {
                Log("keys.txt가 exe 폴더에 없습니다. keys.txt를 exe와 같은 폴더에 넣어주세요 (wud/wux 입력에는 필요합니다).", LogLevel.Error);
                return;
            }

            var rows = await RepackService.PeekFileAsync(path, KeysPath, CancellationToken.None);

            foreach (var row in rows)
                Entries.Add(row);

            Log($"{Path.GetFileName(path)} — {rows.Count}개 타이틀 추가됨.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Log($"'{path}' 추가 실패: {ex.Message}", LogLevel.Error);
        }
    }

    public void AddFolder(string folderPath)
    {
        if (IsDuplicate(folderPath))
            return;

        try
        {
            var row = RepackService.PeekFolder(folderPath);

            Entries.Add(row);
        }
        catch (Exception ex)
        {
            Log($"'{folderPath}' 추가 실패: {ex.Message}", LogLevel.Error);
        }
    }

    private bool IsDuplicate(string path)
    {
        return Entries.Any(e => string.Equals(e.FilePath, path, StringComparison.OrdinalIgnoreCase));
    }

    private void RemoveSelected()
    {
        if (SelectedEntry is not null)
            Entries.Remove(SelectedEntry);
    }

    private void RemoveAll() => Entries.Clear();

    public async Task StartAsync(BuildMode mode)
    {
        if (!Validate(mode, out string error))
        {
            Log(error, LogLevel.Error);
            return;
        }

        _currentMode = mode;
        NotifyButtonStates();

        using (BeginWork())
        {
            try
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
                await ExecuteAsync(mode, _cts.Token);
            }
            finally
            {
                ProgressPct = 0;
                _currentMode = null;
                NotifyButtonStates();
            }
        }
    }

    public void Cancel() => _cts.Cancel();

    private async Task ExecuteAsync(BuildMode mode, CancellationToken ct)
    {
        string unpackedRoot = Path.Combine(OutputPath, "unpacked");

        if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedRoot))
        {
            if (!MessageBoxHelper.ShowQuestion("기존 언팩 데이터를 삭제하고 새로 진행할까요?"))
                return;

            Directory.Delete(unpackedRoot, true);
        }

        if (mode == BuildMode.RebuildOnly)
        {
            var scanned = RepackService.ScanUnpacked(OutputPath);
            if (scanned.Count == 0)
            {
                Log("언팩된 데이터가 없습니다.", LogLevel.Error);
                return;
            }

            foreach (var row in scanned)
            {
                var existing = Entries.FirstOrDefault(e => e.TitleIdHex == row.TitleIdHex && e.TitleVersion == row.TitleVersion);
                if (existing?.PatchPath is not null) row.PatchPath = existing.PatchPath;
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
                    await RepackService.UnpackAsync(entriesSnapshot, KeysPath, OutputPath, progress, Log, ct);
                    break;
                case BuildMode.RebuildOnly:
                case BuildMode.FullProcess:
                    await RepackService.RepackAsync(entriesSnapshot, KeysPath, OutputPath, progress, Log, ct);
                    break;
            }

            isCompleted = true;
            ProgressPercent = "100%";

            Log($"완료! 총 소요: {sw.Elapsed:mm\\:ss}", LogLevel.Ok);
            OutputPath.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            Log("작업이 취소되었습니다.", LogLevel.Error);
        }
        catch (Exception ex)
        {
            Log($"오류: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            if (!isCompleted && mode == BuildMode.UnpackOnly && Directory.Exists(unpackedRoot))
            {
                try { Directory.Delete(unpackedRoot, true); } catch { }
            }
        }
    }

    private Action<ProgressInfo> BuildProgressReporter() =>
        info =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressPct = info.Percent;
                ProgressLabel = info.Label;
                ProgressPercent = $"{info.Percent}%";
                ProgressTime = info.TimeInfo;
                ProgressSpeed = info.Speed;
            });
        };

    private bool Validate(BuildMode mode, out string error)
    {
        error = string.Empty;

        if (mode != BuildMode.RebuildOnly && Entries.Count == 0)
        {
            error = "타이틀을 하나 이상 추가하세요.";
            return false;
        }

        if (mode != BuildMode.RebuildOnly && KeysPathRequired && !File.Exists(KeysPath))
        {
            error = "keys.txt가 exe 폴더에 없습니다. keys.txt를 exe와 같은 폴더에 넣어주세요 (wud/wux 입력에는 필요합니다).";
            return false;
        }

        if (string.IsNullOrEmpty(OutputPath))
        {
            error = "작업 폴더를 선택하세요.";
            return false;
        }

        if (mode == BuildMode.RebuildOnly)
        {
            string unpackedPath = Path.Combine(OutputPath, "unpacked");
            if (!Directory.Exists(unpackedPath))
            {
                error = "언팩된 데이터가 없습니다.";
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

    private void Log(string msg, LogLevel level = LogLevel.Info) =>
        Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));

    private async Task BrowseAddFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "본편 / 업데이트 / DLC 파일 선택",
            Filter = "Wii U ROM 파일|*.wud;*.wux;*.wua",
            Multiselect = false,
        };

        if (dlg.ShowDialog() == true)
            await AddFileAsync(dlg.FileName);
    }

    private async Task BrowseAddFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "이미 언팩된 폴더 선택" };
        if (dlg.ShowDialog() == true)
            AddFolder(dlg.FolderName);
    }

    private async Task BrowsePatchForSelected()
    {
        if (SelectedEntry is null)
            return;

        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = $"{SelectedEntry.TitleName}에 적용할 한글패치 폴더 선택" };

        if (dlg.ShowDialog() == true)
            SelectedEntry.PatchPath = dlg.FolderName;
    }

    private async Task BrowseOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.FolderName;
    }
}