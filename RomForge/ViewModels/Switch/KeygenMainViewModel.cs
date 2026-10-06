using Common;
using Common.WPF.ViewModels;
using NSW.WPF.ViewModels;
using RomForge.Core.Models;
using RomForge.Core.Services.Switch;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.ViewModels.Switch;

public class KeygenMainViewModel : ToolTabViewModel
{
    #region Fields & Properties

    private readonly Stopwatch _totalSw = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    private int _progressPct;
    private string _progressLabel = string.Empty;
    private string _progressPercent = "0%";
    private string _progressTime = string.Empty;
    private string _progressSpeed = string.Empty;

    public int ProgressPct
    {
        get => _progressPct;
        set
        {
            _progressPct = value;
            MainViewModel.SetTaskbarProgress(_progressPct);
            OnPropertyChanged();
        }
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

    #endregion

    #region Constructor

    public KeygenMainViewModel()
    {

    }

    #endregion

    #region Public Methods

    public async Task ExecuteStartWorkAsync(IList<GameFile> gameFiles)
    {
        if (IsLocked) 
            return;

        if (!ValidateAndGetInputs(gameFiles, out var inputPaths, out string errorMsg))
        {
            Log(errorMsg, LogLevel.Error);
            return;
        }

        if (gameFiles.Any(f => f.IsKeyMissing))
            ProgressLabel = Res.Main_Log_Recalculating;

        _totalSw.Restart();

        using (BeginWork())
        {
            try
            {
                _cts = new CancellationTokenSource();
                var progress = BuildProgressReporter();

                var results = await Task.Run(async () =>
                    await NspRecryptService.Recrypt(inputPaths, true, progress, Log, _cts.Token), _cts.Token);

                if (results != null && results.Count > 0)
                {
                    Log(string.Format(Res.Main_Log_AllComplete, _totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);
                    Log(Res.Main_Msg_Done);
                }
            }
            catch (OperationCanceledException)
            {
                Log($"작업이 취소되었습니다.", LogLevel.Error);
                ProgressPct = 0;                
            }
            catch (Exception ex)
            {
                Log($"{Res.Log_Error}: {ex.Message}", LogLevel.Error);
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
            }
            finally
            {
                CleanupTask();
            }
        }
    }

    public void Cancel() => _cts?.Cancel();

    #endregion

    #region Private Methods

    private Progress<ProgressInfo> BuildProgressReporter() =>
        new(info =>
        {
            ProgressPct = info.Percent;
            ProgressLabel = info.Label;
            ProgressPercent = $"{info.Percent}%";
            ProgressTime = info.TimeInfo;
            ProgressSpeed = info.Speed;
        });

    private void CleanupTask()
    {
        _cts?.Dispose();
        _cts = null;
        ProgressLabel = string.Empty;
        ProgressPercent = "0%";
        ProgressTime = string.Empty;
        ProgressSpeed = string.Empty;
    }

    private static bool ValidateAndGetInputs(IList<GameFile> gameFiles, out List<string> inputPaths, out string errorMsg)
    {
        inputPaths = [];
        errorMsg = string.Empty;

        if (gameFiles.Count == 0)
        {
            errorMsg = Res.Main_Err_NoFiles;

            return false;
        }

        if (gameFiles.Any(f => f.IsKeyMissing))
        {
            errorMsg = Res.Main_Err_NoKeys;

            return false;
        }

        string[] allowedExts = [".nsp", ".nsz", ".xci", ".xcz"];
        inputPaths = [.. gameFiles
            .Where(f => allowedExts.Contains(Path.GetExtension(f.FilePath).ToLowerInvariant()))
            .Select(f => f.FilePath)];

        if (inputPaths.Count == 0)
        {
            errorMsg = Res.Main_Err_NoFiles;
            return false;
        }

        return true;
    }

    public void Log(string msg, LogLevel level = LogLevel.Info, string titleId = "") => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));

    #endregion
}