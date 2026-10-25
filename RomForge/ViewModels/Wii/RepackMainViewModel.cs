using Common;
using NSW.Core.Enums;
using NSW.WPF.Services;
using NSW.WPF.UI;
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
    private GameDisplay? _gameInfo;
    private ImageSource? _gameIcon;
    private PatchDisplay? _patchInfo;
    private int _discVersion;
    private int _patchVersion;
    private string _inputPath = string.Empty;
    private string _patchPath = string.Empty;
    private string _outputPath = string.Empty;

    private OutputFormat _outputFormat = OutputFormat.Iso;

    public OutputFormat OutputFormat
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

            if (string.IsNullOrWhiteSpace(InputPath))
                _ = RefreshUnpackedFolderAsync();
        }
    }

    public GameDisplay? GameInfo
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

    public PatchDisplay? PatchInfo
    {
        get => _patchInfo;
        private set
        {
            _patchInfo = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchInfoVisibility));
        }
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
                AppendLog($"언팩을 시작합니다. → {unpackedPath}", LogLevel.Highlight);

                int count = await Task.Run(() => IsoUnpacker.Unpack(source, unpackedPath, progress, ct), ct);

                AppendLog($"파일 {count:N0}개를 풀었습니다.", LogLevel.Ok);
            }
            else
                await ProduceAsync(mode, unpackedPath, sw, tempOutputs, ct);

            isCompleted = true;
            ProgressPercent = "100%";
            MainViewModel.SetTaskbarProgress(100);

            AppendLog($"완료! 총 소요: {sw.Elapsed:mm\\:ss}", LogLevel.Ok);
            await SoundPlayer.PlayCompleteAsync();
            OutputPath.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            AppendLog("작업이 취소되었습니다.", LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            MainViewModel.SetTaskbarProgress(0);
        }
        catch (Exception ex)
        {
            AppendLog($"오류: {ex.Message}", LogLevel.Error);
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
        string prepareLabel = overlay != null ? $"패치 적용 중: {target}" : $"파티션 해시 계산 중: {target}";
        string convertLabel = format switch
        {
            OutputFormat.Wbfs => $"WBFS 압축 중: {target}",
            OutputFormat.Rvz => $"RVZ 압축 중: {target}",
            _ => $"ISO 생성 중: {target}"
        };
        PatchResult result;

        void prepareProgress(double value) => SetProgress(value, prepareLabel, sw);
        void convertProgress(double value) => SetProgress(value, convertLabel, sw);

        tempOutputs.Add(finalPath);
        SetProgress(0, prepareLabel, sw);
        
        if (fromFolder)
        {
            AppendLog($"리팩 시작: {target}", LogLevel.Highlight);

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
            AppendLog($"스트리밍 기반 리팩 시작: {target}", LogLevel.Highlight);

            string source = InputPath;
            IReadOnlyDictionary<string, string> replacements = overlay ?? new Dictionary<string, string>();

            if (format == OutputFormat.Iso)
            {
                void rebuildProgress(double value) => SetProgress(value, $"리팩 중: {Path.GetFileName(source)}", sw);

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
                AppendLog($"경고: {warning}", LogLevel.Error);
        }
    }

    private void LogPatchEntry(PatchEntry entry)
    {
        if (entry.Applied)
            AppendLog($"교체: {entry.Path}", LogLevel.Info);
        else
            AppendLog($"디스크에 없음: {entry.Path}", LogLevel.Error);
    }

    private void LogPatchResult(PatchResult result)
    {
        if (result.Total == 0)
            return;

        int failed = result.Total - result.Applied;

        AppendLog($"패치 결과: 총 {result.Total:N0}개 중 {result.Applied:N0}개 완료, {failed:N0}개 실패", failed == 0 ? LogLevel.Ok : LogLevel.Highlight);
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
                    AppendLog($"디스크 정보를 읽을 수 없습니다: {ex.Message}", LogLevel.Error);
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
            PatchInfo = PatchDisplay.Failed("폴더, zip, 7z, xml만 지정할 수 있습니다.");

            AppendLog($"'{Path.GetFileName(path)}'는 지원하지 않는 형식입니다 (폴더, zip, 7z, xml만 가능).", LogLevel.Error);
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
                        PatchInfo = PatchDisplay.Failed("압축 파일을 열지 못했습니다.");

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
                AppendLog($"패치 정보를 읽었습니다: {patch.Replacements.Count}개 파일 교체, 경고 {patch.Warnings.Count}건", LogLevel.Info);
            }
            catch (OperationCanceledException)
            {
                if (version == _patchVersion)
                {
                    ReleaseWorkspace();

                    PatchInfo = PatchDisplay.Failed("취소되었습니다.");

                    AppendLog("패치 읽기가 취소되었습니다.", LogLevel.Error);
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
                    AppendLog($"패치를 읽을 수 없습니다: {ex.Message}", LogLevel.Error);
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
            AppendLog("비밀번호 입력이 취소되었습니다.", LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            return null;
        }

        SetProgress(0, "패치 압축 해제 중...", sw);

        return await RiivolutionWorkspace.ExtractAsync(path, opened.Value.Archive, value => SetProgress(value, "패치 압축 해제 중...", sw), ct);
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
            ProgressTime = $"{sw.Elapsed:mm\\:ss} 경과";
        });
    }

    private bool Validate(BuildMode mode, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            error = "작업 폴더를 선택하세요.";
            return false;
        }

        string? gameId = _disc?.GameId;

        if (mode == BuildMode.RebuildOnly)
        {
            string unpackedPath = Path.Combine(OutputPath, "unpacked");

            if (!Directory.Exists(unpackedPath))
            {
                error = "언팩된 데이터가 없습니다.";
                return false;
            }

            var folderInfo = FolderInfo.TryRead(unpackedPath);

            if (folderInfo == null)
            {
                error = "언팩 데이터가 이전 버전 형식이거나 손상됐습니다. 다시 언팩해 주세요.";
                return false;
            }

            gameId = folderInfo.GameId;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(InputPath) || !File.Exists(InputPath))
            {
                error = "원본 디스크 파일을 선택하세요.";
                return false;
            }

            if (_disc == null)
            {
                error = "Wii 디스크 정보를 읽지 못했습니다. 원본 디스크를 확인하세요.";
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
                error = "패치 정보를 읽지 못했습니다. 패치 경로를 확인하세요.";
                return false;
            }

            if (gameId != null && !_patch.MatchesDisc(gameId))
            {
                error = $"패치 대상 게임 ID({string.Join(", ", _patch.GameIds)})와 디스크({gameId})가 맞지 않습니다.";
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
                AppendLog($"언팩 폴더 정보를 읽을 수 없습니다: {ex.Message}", LogLevel.Error);
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
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.FolderName;
    }
}