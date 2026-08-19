using Common;
using Common.WPF.ViewModels;
using NSW.Core;
using NSW.WPF.Services;
using NSW.WPF.ViewModels;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Models.Switch;
using RomForge.Core.Services.Switch;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.ViewModels.Switch;

public class MergeMainViewModel : ToolTabViewModel
{
    #region Fields & Properties

    private readonly Stopwatch _totalSw = new();
    private CancellationTokenSource? _cts;

    private string _outputPath = string.Empty;
    private MergeMode? _currentMode;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    private int _progressPct;
    private string _progressLabel = string.Empty;
    private string _progressPercent = string.Empty;
    private string _progressTime = string.Empty;
    private string _progressSpeed = string.Empty;
    private SwitchOutputFormat _outputFormat = SwitchOutputFormat.NSP;

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

    public string OutputPath
    {
        get => _outputPath;
        set { _outputPath = value; OnPropertyChanged(); }
    }

    public SwitchOutputFormat OutputFormat
    {
        get => _outputFormat;
        set { _outputFormat = value; OnPropertyChanged(); }
    }

    public bool IsMergeRunning => IsLocked && _currentMode == MergeMode.Merge;

    public bool IsSplitRunning => IsLocked && _currentMode == MergeMode.Split;

    public bool MergeEnabled => !IsLocked || _currentMode == MergeMode.Merge;

    public bool SplitEnabled => !IsLocked || _currentMode == MergeMode.Split;

    public bool IsCompressionOptionVisible => CompressLevel > 2;

    public int CompressLevel
    {
        get => AppConfig.Instance.Switch.CompressLevel;
        set
        {
            AppConfig.Instance.Switch.CompressLevel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCompressionOptionVisible));
        }
    }

    public bool VerifyCompress
    {
        get => AppConfig.Instance.Switch.VerifyCompress;
        set { AppConfig.Instance.Switch.VerifyCompress = value; OnPropertyChanged(); }
    }

    public bool UseBlockMode
    {
        get => AppConfig.Instance.Switch.UseBlockMode;
        set
        {
            AppConfig.Instance.Switch.UseBlockMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UseBlocklessMode));
        }
    }

    public bool UseBlocklessMode
    {
        get => AppConfig.Instance.Switch.UseBlocklessMode;
        set
        {
            AppConfig.Instance.Switch.UseBlocklessMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UseBlockMode));
        }
    }

    public static bool ForceKeyGen0
    {
        get => AppConfig.Instance.Switch.ForceKeyGen0;
        set => AppConfig.Instance.Switch.ForceKeyGen0 = value;
    }

    public ICommand OpenWorkSpaceCommand { get; }

    public event EventHandler SettingsClicked;

    #endregion

    #region Constructor

    public MergeMainViewModel()
    {
        OutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
        OpenWorkSpaceCommand = new RelayCommand(_ => ExecuteOpenWorkSpace());

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsLocked))
                NotifyButtonStates();
        };
    }

    #endregion

    #region Public Methods

    public async Task MergeAsync(IList<GameFile> gameFiles)
    {
        if (!ValidateMergeInputs(gameFiles, out var inputPaths, out var outputDir, out string errorMsg))
        {
            Log(errorMsg, LogLevel.Error);
            return;
        }

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        _currentMode = MergeMode.Merge;
        NotifyButtonStates();

        _totalSw.Restart();
        using (BeginWork())
        {
            try
            {
                _cts = new CancellationTokenSource();

                var progress = BuildProgressReporter();
                int compressLevel = GetCompressLevel();

                var keySet = KeySetProvider.Instance.KeySet.Clone();
                var results = new List<string>();
                bool useCompression = compressLevel > 0;

                List<string> output =
                    OutputFormat == SwitchOutputFormat.XCI
                    ? await XciMergeService.RunMergeAll(inputPaths, outputDir, useCompression, compressLevel, UseBlockMode, VerifyCompress, ForceKeyGen0, keySet.Clone(), progress, Log, _cts.Token)
                    : await NspMergeService.RunMergeAll(inputPaths, outputDir, useCompression, compressLevel, UseBlockMode, VerifyCompress, ForceKeyGen0, keySet.Clone(), progress, Log, _cts.Token);

                results.AddRange(output);

                if (results?.Count > 0)
                {
                    Log(string.Format(Res.Main_Log_AllComplete, _totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);
                    Log(Res.Main_Msg_Done);

                    outputDir.OpenFolder();
                }
            }
            catch (OperationCanceledException)
            {
                Log($"병합이 취소되었습니다.", LogLevel.Error);
            }
            catch (Exception ex)
            {
                Log($"{Res.Log_Error}: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                ProgressPct = 0;
            }
        }

        _currentMode = null;
        NotifyButtonStates();
    }

    public async Task SplitAsync(IList<GameFile> gameFiles)
    {
        string outputDir = OutputPath?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(outputDir))
        {
            Log(Res.Main_Err_NoOutput, LogLevel.Error);
            return;
        }

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        _currentMode = MergeMode.Split;
        NotifyButtonStates();

        _totalSw.Restart();
        using (BeginWork())
        {
            try
            {
                _cts = new CancellationTokenSource();
                var progress = BuildProgressReporter();

                int compressLevel = GetCompressLevel();
                int resultCount = 0;

                for (int i = 0; i < gameFiles.Count; i++)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    resultCount += await SwitchSplitService.Split(gameFiles[i].FilePath, outputDir, compressLevel, UseBlockMode, VerifyCompress, ForceKeyGen0, OutputFormat == SwitchOutputFormat.XCI, i + 1, gameFiles.Count, progress, Log, _cts.Token);
                }

                Log(string.Format(Res.Main_Log_AllComplete, _totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);

                if (resultCount > 0)
                    Log(Res.Main_Msg_SplitDone);
            }
            catch (OperationCanceledException)
            {
                Log($"분리가 취소되었습니다.", LogLevel.Error);
            }
            catch (Exception ex)
            {
                Log($"{Res.Log_Error}: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                ProgressPct = 0;
            }
        }

        _currentMode = null;
        NotifyButtonStates();
    }

    public void Cancel() => _cts?.Cancel();

    #endregion

    #region Private Methods

    private Progress<ProgressInfo> BuildProgressReporter() =>
        new(info => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            ProgressPct = info.Percent;
            ProgressLabel = info.Label;
            ProgressPercent = $"{info.Percent}%";
            ProgressTime = info.TimeInfo;
            ProgressSpeed = info.Speed;
        }));

    private int GetCompressLevel()
    {
        int level = CompressLevel;

        return level < 3 ? 0 : level;
    }

    private void NotifyButtonStates()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(IsMergeRunning));
            OnPropertyChanged(nameof(IsSplitRunning));
            OnPropertyChanged(nameof(MergeEnabled));
            OnPropertyChanged(nameof(SplitEnabled));
        });
    }

    private bool ValidateMergeInputs(IList<GameFile> gameFiles, out List<string> inputPaths, out string outputDir, out string errorMsg)
    {
        inputPaths = [];
        outputDir = string.Empty;
        errorMsg = string.Empty;

        if (gameFiles.Any(f => f.IsKeyMissing)) 
        { 
            errorMsg = Res.Main_Err_NoKeys; 
            return false; 
        }

        outputDir = OutputPath?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(outputDir)) 
        { 
            errorMsg = Res.Main_Err_NoOutput; 
            return false;
        }

        inputPaths = [.. gameFiles.Select(f => f.FilePath)];

        return true;
    }

    private void ExecuteOpenWorkSpace()
    {
        var path = OutputPath?.Trim();

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) 
            return;

        path?.OpenFolder();
    }

    public void Log(string msg, LogLevel level = LogLevel.Info, string titleId = "") => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));

    public void NavigateSettings() => SettingsClicked?.Invoke(this, EventArgs.Empty);

    #endregion

    private enum MergeMode { Merge, Split }
}