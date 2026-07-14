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

    private RepackOutputFormat _outputFormat = RepackOutputFormat.Wua;
    public RepackOutputFormat OutputFormat
    {
        get => _outputFormat;
        set { _outputFormat = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsWuaFormat)); OnPropertyChanged(nameof(IsWupFormat)); }
    }

    public bool IsWuaFormat
    {
        get => OutputFormat == RepackOutputFormat.Wua;
        set { if (value) OutputFormat = RepackOutputFormat.Wua; }
    }

    public bool IsWupFormat
    {
        get => OutputFormat == RepackOutputFormat.Wup;
        set { if (value) OutputFormat = RepackOutputFormat.Wup; }
    }

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

    /// <summary>본편: wud/wux/wua 파일. wua가 여러 타이틀을 번들하고 있으면 그 안에서만 자체 분류를 신뢰한다.</summary>
    public ICommand BrowseBaseFileCommand { get; }

    /// <summary>본편: 폴더(언팩된 폴더 또는 WUP 폴더). title ID와 무관하게 무조건 본편으로 취급.</summary>
    public ICommand BrowseBaseFolderCommand { get; }

    /// <summary>업데이트: 폴더만. title ID와 무관하게 무조건 업데이트로 취급. 기존 업데이트는 교체.</summary>
    public ICommand BrowseUpdateCommand { get; }

    /// <summary>DLC: 폴더만, 여러 개 가능. title ID와 무관하게 무조건 DLC로 취급.</summary>
    public ICommand BrowseDlcCommand { get; }

    public ICommand BrowsePatchForSelectedCommand { get; }

    public ICommand RemoveSelectedCommand { get; }

    public ICommand RemoveAllCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public RepackMainViewModel()
    {
        OutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

        BrowseBaseFileCommand = new RelayCommand(async _ => await BrowseBaseFile());
        BrowseBaseFolderCommand = new RelayCommand(async _ => await BrowseBaseFolder());
        BrowseUpdateCommand = new RelayCommand(async _ => await BrowseUpdate());
        BrowseDlcCommand = new RelayCommand(async _ => await BrowseDlc());

        BrowsePatchForSelectedCommand = new RelayCommand(async _ => await BrowsePatchForSelected(), _ => HasSelection);
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
    }

    private bool IsDuplicate(string path) => Entries.Any(e => string.Equals(e.FilePath, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>role을 강제 지정해서 목록에 추가한다. 본편/업데이트는 기존 것을 교체하고, DLC는 그냥 추가한다.</summary>
    private void AddWithRole(TitleInputEntry row, TitleRole role)
    {
        row.Role = role;

        if (role is TitleRole.Base or TitleRole.Update)
        {
            var existing = Entries.FirstOrDefault(e => e.Role == role);

            if (existing is not null)
            {
                Entries.Remove(existing);
                Log($"기존 {(role == TitleRole.Base ? "본편" : "업데이트")}을(를) 새 항목으로 교체합니다.", LogLevel.Info);
            }
        }

        Entries.Add(row);
    }

    private async Task BrowseBaseFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "본편 파일 선택",
            Filter = "Wii U ROM 파일|*.wud;*.wux;*.wua",
            Multiselect = false,
        };

        if (dlg.ShowDialog() != true)
            return;

        string path = dlg.FileName;

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

            if (isWua && rows.Count > 1)
            {
                // WUA 하나에 여러 타이틀이 정상적으로 번들된 경우만 title ID 기준 자체 분류를 신뢰한다.
                foreach (var row in rows)
                    AddWithRole(row, row.Role);

                Log($"{Path.GetFileName(path)} — {rows.Count}개 타이틀 추가됨.", LogLevel.Info);
            }
            else
            {
                // wud/wux, 또는 단일 타이틀 wua는 title ID를 신뢰할 수 없으니 무조건 본편으로 지정한다.
                AddWithRole(rows[0], TitleRole.Base);
            }
        }
        catch (Exception ex)
        {
            Log($"'{path}' 추가 실패: {ex.Message}", LogLevel.Error);
        }
    }

    private async Task BrowseBaseFolder() => await BrowseFolderWithRole("본편 폴더 선택", TitleRole.Base);

    private async Task BrowseUpdate() => await BrowseFolderWithRole("업데이트 폴더 선택", TitleRole.Update);

    private async Task BrowseDlc() => await BrowseFolderWithRole("DLC 폴더 선택", TitleRole.Dlc);

    private async Task BrowseFolderWithRole(string title, TitleRole role)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = title };

        if (dlg.ShowDialog() != true)
            return;

        string path = dlg.FolderName;

        if (IsDuplicate(path))
            return;

        try
        {
            var row = RepackService.PeekFolder(path);

            AddWithRole(row, role);
        }
        catch (Exception ex)
        {
            Log($"'{path}' 추가 실패: {ex.Message}", LogLevel.Error);
        }

        await Task.CompletedTask;
    }

    private void RemoveSelected()
    {
        if (SelectedEntry is not null)
            Entries.Remove(SelectedEntry);
    }

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

                if (existing is not null)
                {
                    if (existing.PatchPath is not null) row.PatchPath = existing.PatchPath;
                    row.Role = existing.Role;
                }
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
                    await RepackService.RepackAsync(entriesSnapshot, KeysPath, OutputPath, OutputFormat, progress, Log, ct);
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

    private async Task BrowsePatchForSelected()
    {
        if (SelectedEntry is null)
            return;

        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = $"{SelectedEntry.TitleName}에 적용할 한글패치 폴더 선택" };

        if (dlg.ShowDialog() == true)
            SelectedEntry.PatchPath = dlg.FolderName;

        await Task.CompletedTask;
    }

    private async Task BrowseOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.FolderName;

        await Task.CompletedTask;
    }
}