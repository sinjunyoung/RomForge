using Common;
using Common.WPF.ViewModels;
using DolphinTool.Core.Models;
using DolphinTool.Core.Services.Wii;
using NSW.WPF.Services;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Services.Wii;
using RomForge.Core.UI.Command;
using RomForge.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Wii;

public class WiiMainViewModel : ToolTabViewModel
{
    private CancellationTokenSource _cts = new();
    private CancellationTokenSource _patchCts = new();
    private RiivolutionWorkspace? _workspace;
    private RiivolutionPatchSet? _patch;
    private WiiDiscInfo? _disc;
    private WiiDiscDisplay? _discDisplay;
    private WiiPatchDisplay? _patchDisplay;
    private int _discVersion;
    private int _patchVersion;
    private string _inputPath = string.Empty;
    private string _patchPath = string.Empty;
    private string _outputPath = string.Empty;
    private int _progressPct;
    private string _progressLabel = "대기 중...";
    private string _progressPercent = string.Empty;
    private string _progressTime = "00:00 경과";

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public string InputPath
    {
        get => _inputPath;
        set
        {
            _inputPath = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(InputHintVisibility));

            _ = RefreshDiscAsync();
        }
    }

    public string PatchPath
    {
        get => _patchPath;
        set
        {
            if (_patchPath == value)
                return;

            _patchPath = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchHintVisibility));

            _ = RefreshPatchAsync();
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            _outputPath = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputHintVisibility));

            AppConfig.Instance.OutputFolders.WiiOutputPath = value;
        }
    }

    public WiiDiscDisplay? DiscInfo
    {
        get => _discDisplay;
        private set
        {
            _discDisplay = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(DiscInfoVisibility));
        }
    }

    public WiiPatchDisplay? PatchInfo
    {
        get => _patchDisplay;
        private set
        {
            _patchDisplay = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchInfoVisibility));
        }
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

    public Visibility InputHintVisibility => string.IsNullOrEmpty(InputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PatchHintVisibility => string.IsNullOrEmpty(PatchPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DiscInfoVisibility => DiscInfo != null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PatchInfoVisibility => PatchInfo != null ? Visibility.Visible : Visibility.Collapsed;

    public ICommand BrowseInputCommand { get; }

    public ICommand BrowsePatchFolderCommand { get; }

    public ICommand BrowsePatchFileCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public WiiMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.WiiOutputPath)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output")
            : AppConfig.Instance.OutputFolders.WiiOutputPath;

        BrowseInputCommand = new RelayCommand(_ => BrowseInput());
        BrowsePatchFolderCommand = new RelayCommand(_ => BrowsePatchFolder());
        BrowsePatchFileCommand = new RelayCommand(_ => BrowsePatchFile());
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());

        AppDomain.CurrentDomain.ProcessExit += (_, _) => _workspace?.Dispose();
    }

    public void Cancel()
    {
        _cts.Cancel();
        _patchCts.Cancel();
    }

    public async Task RunAsync()
    {
        if (!Validate(out string error))
        {
            Log(error, LogLevel.Error);
            return;
        }

        using (BeginWork())
        {
            _cts.Dispose();

            _cts = new CancellationTokenSource();

            await ExecuteAsync(_cts.Token);
        }
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        string source = InputPath;
        string patchRoot = _workspace!.RootPath;
        string output = Utils.GetUniqueFilePath(Path.Combine(OutputPath, $"{Path.GetFileNameWithoutExtension(source)}_Riivolution.iso"));
        var sw = Stopwatch.StartNew();
        void progress(double value) => SetProgress(value, $"패치 적용 중: {Path.GetFileName(source)}", sw);
        bool isCompleted = false;

        SetProgress(0, $"패치 적용 중: {Path.GetFileName(source)}", sw);

        try
        {
            Directory.CreateDirectory(OutputPath);

            Log($"Riivolution 패치 적용을 시작합니다. → {Path.GetFileName(output)}", LogLevel.Info);

            var warnings = await Task.Run(() => WiiIsoRebuilder.RebuildWithRiivolution(source, output, patchRoot, null, progress, ct), ct);

            foreach (string warning in warnings)
                Log($"경고: {warning}", LogLevel.Highlight);

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
            if (!isCompleted && File.Exists(output))
            {
                try { File.Delete(output); } catch { }
            }

            ProgressPct = 0;
            ProgressLabel = "대기 중...";
        }
    }

    private async Task RefreshDiscAsync()
    {
        int version = ++_discVersion;
        string path = InputPath;
        WiiDiscInfo? info = null;

        if (File.Exists(path))
        {
            try
            {
                info = await Task.Run(() => WiiDiscInfoReader.Read(path));
            }
            catch (Exception ex)
            {
                if (version == _discVersion)
                    Log($"디스크 정보를 읽을 수 없습니다: {ex.Message}", LogLevel.Error);
            }
        }

        if (version != _discVersion)
            return;

        _disc = info;

        DiscInfo = info == null ? null : WiiDiscDisplay.From(info);

        RebuildPatchDisplay();
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
            PatchInfo = WiiPatchDisplay.Failed("폴더, zip, 7z, xml만 지정할 수 있습니다.");

            Log($"'{Path.GetFileName(path)}'는 지원하지 않는 형식입니다 (폴더, zip, 7z, xml만 가능).", LogLevel.Error);

            return;
        }

        _patchCts = new CancellationTokenSource();

        var ct = _patchCts.Token;
        var sw = Stopwatch.StartNew();

        using (BeginWork())
        {
            try
            {
                var workspace = await OpenWorkspaceAsync(path, sw, ct);

                if (workspace == null)
                {
                    if (version == _patchVersion)
                        PatchInfo = WiiPatchDisplay.Failed("압축 파일을 열지 못했습니다.");

                    return;
                }

                if (version != _patchVersion)
                {
                    workspace.Dispose();

                    return;
                }

                _workspace = workspace;

                ProgressLabel = "패치 정보 분석 중...";

                var patch = await Task.Run(() => RiivolutionParser.Parse(workspace.RootPath), ct);

                if (version != _patchVersion)
                    return;

                _patch = patch;

                RebuildPatchDisplay();

                Log($"패치 정보를 읽었습니다: {patch.Replacements.Count}개 파일 교체, 경고 {patch.Warnings.Count}건", LogLevel.Info);
            }
            catch (OperationCanceledException)
            {
                if (version == _patchVersion)
                {
                    ReleaseWorkspace();

                    PatchInfo = WiiPatchDisplay.Failed("취소되었습니다.");

                    Log("패치 읽기가 취소되었습니다.", LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                if (version == _patchVersion)
                {
                    ReleaseWorkspace();

                    PatchInfo = WiiPatchDisplay.Failed(ex.Message);

                    Log($"패치를 읽을 수 없습니다: {ex.Message}", LogLevel.Error);
                }
            }
            finally
            {
                if (version == _patchVersion)
                {
                    ProgressPct = 0;
                    ProgressPercent = string.Empty;
                    ProgressLabel = "대기 중...";
                }
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
            Log("비밀번호 입력이 취소되었습니다.", LogLevel.Error);

            return null;
        }

        SetProgress(0, "패치 압축 해제 중...", sw);

        return await RiivolutionWorkspace.ExtractAsync(path, opened.Value.Archive, value => SetProgress(value, "패치 압축 해제 중...", sw), ct);
    }

    private void RebuildPatchDisplay()
    {
        if (_workspace == null || _patch == null)
            return;

        PatchInfo = WiiPatchDisplay.From(_workspace.Kind, _patch, _disc?.GameId);
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
            ProgressTime = $"{sw.Elapsed:mm\\:ss} 경과";
        });
    }

    private bool Validate(out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(InputPath) || !File.Exists(InputPath))
        {
            error = "원본 디스크 파일을 선택하세요.";
            return false;
        }

        if (_disc == null)
        {
            error = "Wii 디스크 정보를 읽지 못했습니다. 원본 파일을 확인하세요.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(PatchPath))
        {
            error = "Riivolution 패치(폴더, zip, 7z, xml)를 지정하세요.";
            return false;
        }

        if (_workspace == null || _patch == null)
        {
            error = "패치 정보를 읽지 못했습니다. 패치 경로를 확인하세요.";
            return false;
        }

        if (!_patch.MatchesDisc(_disc.GameId))
        {
            error = $"패치 대상 게임 ID({string.Join(", ", _patch.GameIds)})와 원본 디스크({_disc.GameId})가 맞지 않습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            error = "출력 폴더를 선택하세요.";
            return false;
        }

        return true;
    }

    private void BrowseInput()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "원본 Wii 디스크 선택",
            Filter = "Wii 디스크|*.iso;*.wbfs;*.rvz;*.wia"
        };

        if (dlg.ShowDialog() == true)
            InputPath = dlg.FileName;
    }

    private void BrowsePatchFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Riivolution 패치 폴더 선택" };

        if (dlg.ShowDialog() == true)
            PatchPath = dlg.FolderName;
    }

    private void BrowsePatchFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Riivolution 패치 선택",
            Filter = "Riivolution 패치|*.zip;*.7z;*.xml"
        };

        if (dlg.ShowDialog() == true)
            PatchPath = dlg.FileName;
    }

    private void BrowseOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "출력 폴더 선택" };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.FolderName;
    }

    private void Log(string msg, LogLevel level = LogLevel.Info) => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
}