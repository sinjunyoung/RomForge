using Common;
using Common.WPF.ViewModels;
using NSW.Core.Enums;
using NSW.WPF.Services;
using NSW.WPF.UI;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Models.Wii;
using RomForge.Core.Services.Wii;
using RomForge.Core.UI.Command;
using RomForge.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WiiGC.Core.Models;
using WiiGC.Core.Services;
using WiiGC.Core.Services.Wii;

namespace RomForge.ViewModels.Wii;

public class WiiMainViewModel : ToolTabViewModel
{
    private const int RvzCompressionLevel = 5;
    private const int RvzChunkSize = 131072;

    private CancellationTokenSource _cts = new();
    private CancellationTokenSource _patchCts = new();
    private BuildMode? _currentMode;
    private bool _patchLoading;
    private RiivolutionWorkspace? _workspace;
    private RiivolutionPatchSet? _patch;
    private WiiDiscInfo? _disc;
    private WiiGameDisplay? _gameInfo;
    private ImageSource? _gameIcon;
    private WiiPatchDisplay? _patchInfo;
    private int _discVersion;
    private int _patchVersion;
    private string _inputPath = string.Empty;
    private string _patchPath = string.Empty;
    private string _outputPath = string.Empty;
    private int _progressPct;
    private string _progressLabel = "대기 중...";
    private string _progressPercent = string.Empty;
    private string _progressTime = "00:00 경과";
    private WiiOutputFormat _outputFormat = WiiOutputFormat.Iso;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public WiiOutputFormat OutputFormat
    {
        get => _outputFormat;
        set
        {
            _outputFormat = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIsoFormat));
            OnPropertyChanged(nameof(IsWbfsFormat));
            OnPropertyChanged(nameof(IsRvzFormat));
        }
    }

    public bool IsIsoFormat
    {
        get => OutputFormat == WiiOutputFormat.Iso;
        set
        {
            if (value)
                OutputFormat = WiiOutputFormat.Iso;
        }
    }

    public bool IsWbfsFormat
    {
        get => OutputFormat == WiiOutputFormat.Wbfs;
        set
        {
            if (value)
                OutputFormat = WiiOutputFormat.Wbfs;
        }
    }

    public bool IsRvzFormat
    {
        get => OutputFormat == WiiOutputFormat.Rvz;
        set
        {
            if (value)
                OutputFormat = WiiOutputFormat.Rvz;
        }
    }

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

    public WiiGameDisplay? GameInfo
    {
        get => _gameInfo;
        private set
        {
            _gameInfo = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(GameInfoVisibility));
        }
    }

    public ImageSource? GameIcon
    {
        get => _gameIcon;
        private set { _gameIcon = value; OnPropertyChanged(); }
    }

    public WiiPatchDisplay? PatchInfo
    {
        get => _patchInfo;
        private set
        {
            _patchInfo = value;

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

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsLocked))
                NotifyButtonStates();
        };
    }

    public void Cancel()
    {
        _cts.Cancel();
        _patchCts.Cancel();
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
                ProgressLabel = "대기 중...";
                _currentMode = null;
                NotifyButtonStates();
            }
        }
    }

    private async Task ExecuteAsync(BuildMode mode, CancellationToken ct)
    {
        string unpackedPath = Path.Combine(OutputPath, "unpacked");

        if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedPath))
        {
            if (!MessageBoxHelper.ShowQuestion("기존 언팩 데이터를 삭제하고 새로 진행할까요?"))
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
                void progress(double value) => SetProgress(value, $"언팩 중: {Path.GetFileName(source)}", sw);

                SetProgress(0, $"언팩 중: {Path.GetFileName(source)}", sw);

                Log($"언팩을 시작합니다. → {unpackedPath}", LogLevel.Info);

                int count = await Task.Run(() => WiiIsoUnpacker.Unpack(source, unpackedPath, progress, ct), ct);

                Log($"파일 {count:N0}개를 풀었습니다.", LogLevel.Info);
            }
            else
            {
                await ProduceAsync(mode, unpackedPath, sw, tempOutputs, ct);
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
            if (!isCompleted)
            {
                foreach (string path in tempOutputs.Distinct())
                {
                    if (File.Exists(path))
                        try { File.Delete(path); } catch { }
                }

                if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedPath))
                    try { Directory.Delete(unpackedPath, true); } catch { }
            }
        }
    }

    private async Task ProduceAsync(BuildMode mode, string unpackedPath, Stopwatch sw, List<string> tempOutputs, CancellationToken ct)
    {
        string baseName = Path.Combine(OutputPath, Path.GetFileNameWithoutExtension(InputPath) + "_Repack");
        string finalPath = Utils.GetUniqueFilePath(baseName + GetExtension(OutputFormat));
        string source = InputPath;
        var format = OutputFormat;
        IReadOnlyDictionary<string, string> replacements;

        if (mode == BuildMode.FullProcess)
            replacements = _patch!.Replacements;
        else
            replacements = WiiIsoRebuilder.MergeFolderReplacements(unpackedPath, _patch?.Replacements);

        tempOutputs.Add(finalPath);

        SetProgress(0, $"리빌드 중: {Path.GetFileName(source)}", sw);

        Log($"빌드를 시작합니다. → {Path.GetFileName(finalPath)}", LogLevel.Info);

        if (format == WiiOutputFormat.Iso)
        {
            bool reached99 = false;
            bool reached999 = false;
            bool reached100 = false;

            void rebuildProgress(double value)
            {
                SetProgress(value, $"리빌드 중: {Path.GetFileName(source)}", sw);

                if (!reached99 && value >= 0.99)
                {
                    reached99 = true;
                    Log($"[진단] 99.0% 도달: {sw.Elapsed:mm\\:ss\\.f}", LogLevel.Info);
                }

                if (!reached999 && value >= 0.999)
                {
                    reached999 = true;
                    Log($"[진단] 99.9% 도달: {sw.Elapsed:mm\\:ss\\.f}", LogLevel.Info);
                }

                if (!reached100 && value >= 1.0)
                {
                    reached100 = true;
                    Log($"[진단] 100% 도달: {sw.Elapsed:mm\\:ss\\.f}", LogLevel.Info);
                }
            }

            await Task.Run(() => WiiIsoRebuilder.RebuildWithReplacements(source, finalPath, replacements, rebuildProgress, ct), ct);

            Log($"[진단] 코어 반환: {sw.Elapsed:mm\\:ss\\.f}", LogLevel.Info);
        }
        else
        {
            string convertLabel = format == WiiOutputFormat.Wbfs ? $"WBFS 압축 중: {Path.GetFileName(source)}" : $"RVZ 압축 중: {Path.GetFileName(source)}";

            void prepareProgress(double value) => SetProgress(value, $"패치 적용 중: {Path.GetFileName(source)}", sw);
            void convertProgress(double value) => SetProgress(value, convertLabel, sw);

            await Task.Run(() =>
            {
                if (format == WiiOutputFormat.Wbfs)
                    WiiIsoStreamConverter.RebuildToWbfs(source, finalPath, replacements, prepareProgress, convertProgress, ct);
                else
                    WiiIsoStreamConverter.RebuildToRvz(source, finalPath, replacements, RvzCompressionLevel, RvzChunkSize, prepareProgress, convertProgress, ct);
            }, ct);
        }

        if (_patch != null)
        {
            foreach (string warning in _patch.Warnings)
                Log($"경고: {warning}", LogLevel.Highlight);
        }
    }

    private async Task RefreshDiscAsync()
    {
        int version = ++_discVersion;
        string path = InputPath;
        WiiDiscInfo? info = null;

        if (File.Exists(path))
        {
            if (!string.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase))
            {
                Log("현재는 ISO 파일만 지원합니다 (WBFS/RVZ/WIA는 차후 지원 예정).", LogLevel.Error);
            }
            else
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
        }

        if (version != _discVersion)
            return;

        _disc = info;

        GameInfo = info == null ? null : WiiGameDisplay.From(info);
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
            bytes = await WiiCoverArtFetcher.TryGetDiscPngAsync(gameId);
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
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            return image;
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
            PatchInfo = WiiPatchDisplay.Failed("폴더, zip, 7z, xml만 지정할 수 있습니다.");

            Log($"'{Path.GetFileName(path)}'는 지원하지 않는 형식입니다 (폴더, zip, 7z, xml만 가능).", LogLevel.Error);

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
                _patchLoading = false;

                if (version == _patchVersion)
                {
                    ProgressPct = 0;
                    ProgressPercent = string.Empty;
                    ProgressLabel = "대기 중...";
                }

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

    private bool Validate(BuildMode mode, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(InputPath) || !File.Exists(InputPath))
        {
            error = "원본 디스크 파일을 선택하세요.";
            return false;
        }

        if (_disc == null)
        {
            error = "Wii 디스크 정보를 읽지 못했습니다. 원본 ISO를 확인하세요.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            error = "작업 폴더를 선택하세요.";
            return false;
        }

        if (mode == BuildMode.UnpackOnly)
            return true;

        if (mode == BuildMode.RebuildOnly && !Directory.Exists(Path.Combine(OutputPath, "unpacked")))
        {
            error = "언팩된 데이터가 없습니다.";
            return false;
        }

        bool hasPatchPath = !string.IsNullOrWhiteSpace(PatchPath);

        if (mode == BuildMode.FullProcess && !hasPatchPath)
        {
            error = "Riivolution 패치(폴더, zip, 7z, xml)를 지정하세요.";
            return false;
        }

        if (hasPatchPath && (_workspace == null || _patch == null))
        {
            error = "패치 정보를 읽지 못했습니다. 패치 경로를 확인하세요.";
            return false;
        }

        if (_patch != null && !_patch.MatchesDisc(_disc.GameId))
        {
            error = $"패치 대상 게임 ID({string.Join(", ", _patch.GameIds)})와 원본 디스크({_disc.GameId})가 맞지 않습니다.";
            return false;
        }

        return true;
    }

    private static string GetExtension(WiiOutputFormat format) => format switch
    {
        WiiOutputFormat.Wbfs => ".wbfs",
        WiiOutputFormat.Rvz => ".rvz",
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
            Title = "원본 Wii 디스크 선택",
            Filter = "Wii 디스크|*.iso"
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
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.FolderName;
    }

    private void Log(string msg, LogLevel level = LogLevel.Info) => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
}