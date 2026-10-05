using _3DS.Core.Crypto;
using _3DS.Core.Enums;
using _3DS.Core.FileSystem;
using _3DS.Core.Models;
using _3DS.Core.Services;
using Common;
using Common.WPF.ViewModels;
using NSW.WPF.UI;
using RomForge.Core.Models;
using RomForge.Core.Models._3DS;
using RomForge.Core.Services._3DS;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace RomForge.ViewModels._3DS;

public class InstallerMainViewModel : ToolTabViewModel
{
    private string _sdPath = string.Empty;
    private string _movablePath = string.Empty;
    private string _statusMessage = "SD 카드와 movable.sed를 선택하세요";
    private double _progress;
    private bool _isLoading;
    private string _progressText = string.Empty;
    private string _progressTime = string.Empty;
    private string _progressSpeed = string.Empty;
    private KeyStore? _keyStore;
    private SdCrypto? _sdCrypto;
    private SdTitleScanner? _scanner;
    private CancellationTokenSource? _extractCts;

    private IEnumerable<ToolTabViewModel> AllTabs => [InstalledTitles, Install];

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    private static readonly string[] MovableSearchPaths =
    [
        "movable.sed",
        @"gm9\out\movable.sed",
    ];

    public InstalledTitlesMainViewModel InstalledTitles { get; }

    public InstallMainViewModel Install { get; } = new();

    public string SdPath
    {
        get => _sdPath;
        set
        {
            _sdPath = value;
            InvalidateSession();
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLoad));
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    public string MovablePath
    {
        get => _movablePath;
        set
        {
            _movablePath = value;
            InvalidateSession();
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLoad));
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string ProgressText
    {
        get => _progressText;
        set { _progressText = value; OnPropertyChanged(); }
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

    public bool IsPathValid => !string.IsNullOrEmpty(SdPath) && !string.IsNullOrEmpty(MovablePath);

    public bool CanLoad => IsPathValid && !_isLoading && InstalledTitles.IsUnlocked;

    public Visibility ProgressPanelVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CancelButtonVisibility => _extractCts != null ? Visibility.Visible : Visibility.Collapsed;

    public bool CanInstall => CanLoad && Install.IsUnlocked && Install.IsNotInstalling;

    public event EventHandler RunNavigateCerts;

    public InstallerMainViewModel()
    {
        InstalledTitles = new InstalledTitlesMainViewModel(msg => StatusMessage = msg);

        Tools.Add(InstalledTitles);
        Tools.Add(Install);

        RegisterChild(InstalledTitles);
        RegisterChild(Install);

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
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "취소되었습니다.";
            MainViewModel.SetTaskbarProgress(0);
            AppendLog("추출이 취소되었습니다.", LogLevel.Error);
        }
        catch (Exception ex)
        {
            StatusMessage = $"오류: {ex.Message}";
            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
            AppendLog($"오류: {ex.Message}", LogLevel.Error);
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
        StatusMessage = "초기화 중...";
        ClearLog();
        AppendLog("3DS 타이틀 스캔을 시작합니다.", LogLevel.Highlight);
        AppendLog($"SD 경로: {SdPath}, Movable 경로: {MovablePath}", LogLevel.Info);

        using (InstalledTitles.BeginWork())
        {
            try
            {
                EnsureInitialized();

                await InstalledTitles.LoadAsync(_keyStore!, _sdCrypto!, _scanner!,
                    (current, total) =>
                    {
                        Progress = (double)current / total * 100;
                        MainViewModel.SetTaskbarProgress(Progress);
                        ProgressText = $"로딩 중... {current} / {total}";
                        onProgress?.Invoke(current, total);
                    }, ct);

                AppendLog($"타이틀 목록 로드 완료.", LogLevel.Ok);
            }
            catch (Exception ex)
            {
                StatusMessage = $"오류: {ex.Message}";
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        SetLoading(false);
        OnPropertyChanged(nameof(CanLoad));
    }

    public async Task RecoverTitleDbAsync(CancellationToken ct = default)
    {
        if (!MessageBoxHelper.ShowQuestion("기존 title.db와 import.db를 삭제하고,\nSD 카드에 설치된 타이틀로 새로 만듭니다.\n\n계속 진행하시겠습니까?"))
            return;

        SetLoading(true);
        StatusMessage = "초기화 중...";
        ClearLog();
        AppendLog("title.db 복구를 시작합니다.", LogLevel.Highlight);
        AppendLog($"SD 경로: {SdPath}, Movable 경로: {MovablePath}", LogLevel.Info);

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
                    Progress = p.total == 0 ? 0 : (double)p.current / p.total * 100;
                    ProgressText = $"스캔 중... {p.current} / {p.total}";
                    MainViewModel.SetTaskbarProgress(Progress);
                });

                void Report(int current, int total) => ((IProgress<(int, int)>)scanProgress).Report((current, total));

                StatusMessage = "title.db 복구 중...";

                var (rebuilt, skipped) = await rebuilder.RebuildAsync(Report, ct);

                StatusMessage = $"복구 완료: {rebuilt}개 타이틀";
                AppendLog($"복구 완료: {rebuilt}개 등록", LogLevel.Ok);

                if (skipped > 0)
                    AppendLog($"{skipped}개는 읽을 수 없어 제외되었습니다. 위 로그를 확인하세요.", LogLevel.Error);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "취소되었습니다.";
                MainViewModel.SetTaskbarProgress(0);
                AppendLog("복구가 취소되었습니다.", LogLevel.Error);
            }
            catch (Exception ex)
            {
                StatusMessage = $"오류: {ex.Message}";
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
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
                Progress = p.Percent;
                MainViewModel.SetTaskbarProgress(Progress);
                ProgressText = $"{p.Percent}%";
                ProgressTime = p.TimeInfo;
                ProgressSpeed = p.Speed;
                StatusMessage = $"추출 중: {selected.ShortDescription} - {p.Label}";
            });

            try
            {
                AppendLog($"타이틀 추출 준비 완료: {selected.ShortDescription} (ID: {selected.TitleId})", LogLevel.Info);
                AppendLog($"출력 경로: {outputPath} (형식: {(asCci ? "CCI" : "CIA")})", LogLevel.Info);

                await RunOperationAsync(_extractCts, $"추출 시작: {selected.ShortDescription}",
                    async ct =>
                    {
                        if (asCci)
                            await extractor.ExtractToCciAsync(selected.Title, outputPath, extractProgress, ct);
                        else
                            await extractor.ExtractToCiaAsync(selected.Title, outputPath, extractProgress, ct);
                    },
                    $"추출 완료: {selected.ShortDescription}");
            }
            catch (CertsBinNotFoundException)
            {
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                RunNavigateCerts?.Invoke(this, EventArgs.Empty);
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
                AppendLog("설정된 타이틀 ID가 비어있거나 올바르지 않습니다.", LogLevel.Error);
                throw new InvalidOperationException("타이틀 ID가 올바르지 않습니다.");
            }

            string tidHigh = selected.TitleId[..8];
            string tidLow = selected.TitleId[8..];
            string titleRoot = Path.Combine(_scanner!.Id1Path, "title", tidHigh, tidLow);

            if (Directory.Exists(titleRoot))
            {
                AppendLog($"이미 동일한 타이틀이 존재합니다. 경로: {titleRoot}", LogLevel.Info);
                var result = MessageBoxHelper.ShowQuestion($"{selected.ShortDescription} 이(가) 이미 설치되어 있습니다.\n재설치하시겠습니까?");

                if (!result)
                {
                    AppendLog("사용자가 재설치를 거부하여 작업을 중단합니다.", LogLevel.Info);
                    return;
                }

                AppendLog("기존에 설치된 타이틀 폴더를 완전히 삭제합니다.", LogLevel.Info);
                Directory.Delete(titleRoot, true);
            }

            SetLoading(true);
            StatusMessage = $"설치 시작: {selected.ShortDescription}";
            AppendLog($"설치 시작: {selected.FilePath}", LogLevel.Highlight);

            try
            {
                var installer = new TitleInstaller(_keyStore!, _sdCrypto!, _scanner!);

                installer.OnLog += msg => AppendLog($"{msg}", LogLevel.Info);

                var installProgress = new Progress<ProgressInfo>(p =>
                {
                    Progress = p.Percent;
                    MainViewModel.SetTaskbarProgress(Progress);
                    selected.Progress = p.Percent;
                    ProgressText = $"{p.Percent}%";
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
                        AppendLog($"지원하지 않는 확장자 포맷입니다: {ext}", LogLevel.Error);
                        throw new NotSupportedException($"지원하지 않는 파일 형식: {ext}");
                }

                StatusMessage = $"설치 완료: {selected.ShortDescription}";
                AppendLog($"설치 완료: {selected.ShortDescription}", LogLevel.Ok);
                AppendLog($"3DS에서 [홈브루 런처]를 이용해, 3ds 폴더 내부의 'custom-install-finalize'를 실행해야 게임 아이콘이 생성됩니다.", LogLevel.Highlight);

                if (selected.ForcedLanguage != Locale3dsLanguage.None)
                {
                    if (selected.LocaleRegionCode == LocaleRegion.None)
                        AppendLog("제품 코드에서 지역을 판별할 수 없어 로케일 고정을 건너뜁니다.", LogLevel.Error);
                    else
                    {
                        await LocaleTxtWriter.WriteAsync(_scanner!.SdRoot, selected.TitleId, selected.ForcedLanguage, ct);
                        AppendLog($"로케일 고정 완료: {LocaleTxtWriter.LanguageToRegion[selected.ForcedLanguage]} {selected.ForcedLanguage} (locale.txt)", LogLevel.Ok);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "취소되었습니다.";
                AppendLog("설치가 취소되었습니다.", LogLevel.Error);
                MainViewModel.SetTaskbarProgress(0);
                if (Directory.Exists(titleRoot))
                    try { Directory.Delete(titleRoot, true); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                StatusMessage = $"오류: {ex.Message}";
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
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
        var found = await Task.Run(() =>
            MovableSearchPaths
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
        {
            Progress = 0;
            ProgressText = string.Empty;
            ProgressTime = string.Empty;
            ProgressSpeed = string.Empty;
        }
    }

    public void AppendLog(string msg, LogLevel level = LogLevel.Info)
    {
        if (Application.Current?.Dispatcher == null)
            return;

        Application.Current.Dispatcher.Invoke(() =>
            LogEntries.Add(new LogEntry { Message = msg, Level = level })
        );
    }

    private void ClearLog()
    {
        if (Application.Current?.Dispatcher == null)
            return;

        Application.Current.Dispatcher.Invoke(() => LogEntries.Clear());
    }
}