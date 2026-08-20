using _3DS.Core.Crypto;
using _3DS.Core.Services;
using Common;
using Common.WPF.ViewModels;
using NSW.Core.Enums;
using NSW.Utils;
using NSW.WPF.Services;
using NSW.WPF.UI;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Models._3DS;
using RomForge.Core.Services._3DS;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels._3DS;

public class RepackMainViewModel : ToolTabViewModel
{
    private CancellationTokenSource _cts = new();
    private BuildMode? _currentMode;
    private readonly RepackService _service;
    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    private string _inputPath = string.Empty;
    private string _patchPath = string.Empty;
    private string _outputPath = string.Empty;
    private int _progressPct;
    private string _progressLabel = "대기 중...";
    private string _progressPercent = string.Empty;
    private string _progressTime = "00:00 경과";
    private string _progressSpeed = string.Empty;
    private TitleViewModel? _romInfo;

    private RepackOutputFormat _outputFormat = RepackOutputFormat.Cci;

    public RepackOutputFormat OutputFormat
    {
        get => _outputFormat;
        set
        {
            _outputFormat = value; OnPropertyChanged();

            OnPropertyChanged(nameof(IsCciFormat));
            OnPropertyChanged(nameof(IsZcciFormat));
            OnPropertyChanged(nameof(IsCiaFormat));
        }
    }

    public bool IsCciFormat
    {
        get => OutputFormat == RepackOutputFormat.Cci;
        set
        {
            if (value)
                OutputFormat = RepackOutputFormat.Cci;
        }
    }

    public bool IsZcciFormat
    {
        get => OutputFormat == RepackOutputFormat.Zcci;
        set
        {
            if (value)
                OutputFormat = RepackOutputFormat.Zcci;
        }
    }

    public bool IsCiaFormat
    {
        get => OutputFormat == RepackOutputFormat.Cia;
        set
        {
            if (value)
                OutputFormat = RepackOutputFormat.Cia;
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
            _ = RefreshRomInfoAsync();
        }
    }

    public string PatchPath
    {
        get => _patchPath;
        set
        {
            _patchPath = value; OnPropertyChanged();
            OnPropertyChanged(nameof(PatchHintVisibility));
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

            AppConfig.Instance.OutputFolders.ThreeDsRepackOutputPath = value;

            if (string.IsNullOrEmpty(InputPath))
                _ = RefreshRomInfoAsync();
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

    public string ProgressSpeed
    {
        get => _progressSpeed;
        set { _progressSpeed = value; OnPropertyChanged(); }
    }

    public TitleViewModel? RomInfo
    {
        get => _romInfo;
        set { _romInfo = value; OnPropertyChanged(); OnPropertyChanged(nameof(RomInfoVisibility)); }
    }

    public Visibility InputHintVisibility => string.IsNullOrEmpty(InputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PatchHintVisibility => string.IsNullOrEmpty(PatchPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RomInfoVisibility => RomInfo != null ? Visibility.Visible : Visibility.Collapsed;

    public bool IsUnpackRunning => IsLocked && _currentMode == BuildMode.UnpackOnly;

    public bool IsRebuildRunning => IsLocked && _currentMode == BuildMode.RebuildOnly;

    public bool IsFullRunning => IsLocked && _currentMode == BuildMode.FullProcess;

    public bool UnpackEnabled => !IsLocked || _currentMode == BuildMode.UnpackOnly;

    public bool RebuildEnabled => !IsLocked || _currentMode == BuildMode.RebuildOnly;

    public bool StartEnabled => !IsLocked || _currentMode == BuildMode.FullProcess;

    public ICommand BrowseInputCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    public RepackMainViewModel()
    {
        _service = new RepackService(Log, () => PatchPath);

        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.ThreeDsRepackOutputPath)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output")
            : AppConfig.Instance.OutputFolders.ThreeDsRepackOutputPath;
        BrowseInputCommand = new RelayCommand(async _ => await BrowseInput());
        BrowseOutputCommand = new RelayCommand(async _ => await BrowseOutput());

        _ = RefreshRomInfoAsync();

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsLocked))
                NotifyButtonStates();
        };
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
        var keyStore = KeyStoreProvider.Instance.KeyStore;
        string unpackedPath = Path.Combine(OutputPath, "unpacked");

        if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedPath))
        {
            if (!MessageBoxHelper.ShowQuestion("기존 언팩 데이터를 삭제하고 새로 진행할까요?"))
                return;
            else
                Directory.Delete(unpackedPath, true);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var progress = BuildProgressReporter();
        string inputFileName = Path.GetFileNameWithoutExtension(InputPath);
        var reporter = new ProgressReporter(inputFileName, string.Empty, 0, progress);
        bool isCompleted = false;
        string? producedPath = null;
        var tempOutputs = new List<string>();
        var tempOutputsLock = new object();

        void TrackOutput(string path)
        {
            lock (tempOutputsLock)
                tempOutputs.Add(path);
        }

        try
        {
            Directory.CreateDirectory(OutputPath);

            switch (mode)
            {
                case BuildMode.UnpackOnly:
                    await Task.Run(() => _service.UnpackAsync(InputPath, unpackedPath, keyStore, reporter.CreateAction(), ct), ct);
                    break;
                case BuildMode.RebuildOnly:
                    producedPath = await Task.Run(() => _service.RepackAsync(unpackedPath, OutputPath, _romInfo?.ShortDescription, RomInfo?.ShortDescriptionChanged == true ? RomInfo.ShortDescription : null, RomInfo?.PublisherChanged == true ? RomInfo.Publisher : null, keyStore, OutputFormat, reporter.CreateAction(), TrackOutput, ct), ct);

                    if (OutputFormat != RepackOutputFormat.Cia)
                    {
                        if (OutputFormat == RepackOutputFormat.Zcci)
                            TrackOutput(Path.ChangeExtension(producedPath, ".zcci"));

                        producedPath = await FinalizeOutputFormatAsync(producedPath, progress, ct);
                    }
                    break;
                case BuildMode.FullProcess:
                    string safeName = NspNameBuilder.SafeFileName(_romInfo?.ShortDescription ?? string.Empty);
                    string fileName = string.IsNullOrEmpty(safeName) ? inputFileName : safeName;
                    string? titleId = _romInfo?.TitleId;
                    string namePart = string.IsNullOrEmpty(titleId) ? fileName : $"{fileName} [{titleId}]";
                    string outputCci = Utils.GetUniqueFilePath(Path.Combine(OutputPath, namePart + "_Repack.cci"));

                    producedPath = await Task.Run(() => _service.RepackDirectAsync(InputPath, outputCci, keyStore, RomInfo?.ShortDescriptionChanged == true ? RomInfo.ShortDescription : null, RomInfo?.PublisherChanged == true ? RomInfo.Publisher : null, OutputFormat, reporter.CreateAction(), TrackOutput, ct), ct);

                    if (OutputFormat != RepackOutputFormat.Cia)
                    {
                        if (OutputFormat == RepackOutputFormat.Zcci)
                            TrackOutput(Path.ChangeExtension(producedPath, ".zcci"));

                        producedPath = await FinalizeOutputFormatAsync(producedPath, progress, ct);
                    }
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
            if (!isCompleted)
            {
                if (mode != BuildMode.UnpackOnly)
                {
                    var toDelete = tempOutputs.Concat(producedPath != null ? [producedPath] : []).Distinct();

                    foreach (string path in toDelete)
                    {
                        if (File.Exists(path))
                            try { File.Delete(path); } catch { }
                    }
                }

                if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedPath))
                    try { Directory.Delete(unpackedPath, true); } catch { }
            }
        }
    }

    private async Task<string> FinalizeOutputFormatAsync(string cciPath, Progress<ProgressInfo> progress, CancellationToken ct)
    {
        if (OutputFormat != RepackOutputFormat.Zcci)
            return cciPath;

        long cciSize = new FileInfo(cciPath).Length;
        var zcciReporter = new ProgressReporter(Path.GetFileNameWithoutExtension(cciPath), string.Empty, cciSize, progress);
        var zcciProgress = new Progress<ProgressInfo>(info => zcciReporter.ReportPercent(info.Percent / 100.0));

        await Z3dsArchiveService.CompressAsync(cciPath, 18, zcciProgress, Log, ct);
        TryDeleteFile(cciPath);

        return Path.ChangeExtension(cciPath, ".zcci");
    }

    private static void TryDeleteFile(string path)
    {
        if (File.Exists(path))
            try { File.Delete(path); } catch { }
    }

    private Progress<ProgressInfo> BuildProgressReporter() =>
        new(info =>
        {
            ProgressPct = info.Percent;
            ProgressLabel = info.Label;
            ProgressPercent = $"{info.Percent}%";
            ProgressTime = info.TimeInfo;
            ProgressSpeed = info.Speed;
        });

    private async Task RefreshRomInfoAsync()
    {
        if (!string.IsNullOrEmpty(InputPath) && File.Exists(InputPath))
            RomInfo = await RomInfoParser.ParseFromFileAsync(InputPath);
        else
            RomInfo = await RomInfoParser.ParseFromUnpackedAsync(OutputPath);
    }

    private bool Validate(BuildMode mode, out string error)
    {
        error = string.Empty;

        if (mode != BuildMode.RebuildOnly && string.IsNullOrEmpty(InputPath))
        {
            error = "원본 파일을 선택하세요.";

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

    private void Log(string msg, LogLevel level = LogLevel.Info) => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));

    private async Task BrowseInput()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "원본 파일 선택",
            Filter = "3DS ROM 파일|*.cci;*.zcci;*.3ds;*.cia"
        };

        if (dlg.ShowDialog() == true)
            InputPath = dlg.FileName;
    }

    private async Task BrowseOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.FolderName;
    }
}