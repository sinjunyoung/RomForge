using Common.Models;
using Common.Services;
using RomForge.Core;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels.Switch;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Res = NSW.Properties.Resources;

namespace RomForge.ViewModels.Switch;

public class Lz4MergeMainViewModel : BatchToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();

    public string OutputPath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(OutputHintVisibility)); AppConfig.Instance.OutputFolders.SwitchLz4MergeOutputPath = value; } } = string.Empty;

    public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public ICommand OpenWorkSpaceCommand { get; }

    public Lz4MergeMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.SwitchLz4MergeOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.SwitchLz4MergeOutputPath;
        OpenWorkSpaceCommand = new RelayCommand(_ => ExecuteOpenWorkSpace());
    }

    public async Task StartAsync(IList<GameFile> gameFiles)
    {
        if (!ValidateInputs(gameFiles, out var inputPaths, out var outputDir, out string errorMsg))
        {
            AppendLog(errorMsg, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        _totalSw.Restart();

        bool canceled = false;
        bool failed = false;

        using (BeginWork())
        {
            try
            {
                CancellationTokenSource = new CancellationTokenSource();

                var token = CancellationTokenSource.Token;
                int compressLevel = AppConfig.Instance.Switch.Lz4CompressLevel;
                var rowsByPath = gameFiles
                    .GroupBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                foreach (var file in gameFiles)
                {
                    file.Progress = 0;
                    file.Status = "대기중";
                }

                var results = await Task.Run(() => Lz4MergeService.RunAsync(inputPaths, outputDir, compressLevel, BeginBatch, paths => CreateJobProgress(rowsByPath, paths), (paths, success) => FinishJob(rowsByPath, paths, success),
                    (msg, level) => AppendLog(msg, level), token), token);

                if (results.Count > 0)
                {
                    AppendLog(string.Format(Res.Main_Log_AllComplete, _totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);
                    AppendLog(Res.Main_Msg_Done);
                    await SoundPlayer.PlayCompleteAsync();
                    outputDir.OpenFolder();
                }
                else
                {
                    await SoundPlayer.PlayErrorAsync();
                }
            }
            catch (OperationCanceledException)
            {
                canceled = true;

                AppendLog("작업이 취소되었습니다.", LogLevel.Error);

                foreach (var file in gameFiles.Where(f => f.Status != "완료" && f.Status != "실패"))
                {
                    file.Status = "취소";
                    file.Progress = 0;
                }

                await SoundPlayer.PlayCancelAsync();
            }
            catch (Exception ex)
            {
                failed = true;

                AppendLog($"{Res.Log_Error}: {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
            }
            finally
            {
                EndBatch(canceled, failed);
            }
        }
    }

    private Progress<ProgressInfo> CreateJobProgress(Dictionary<string, List<GameFile>> rowsByPath, IReadOnlyList<string> paths)
    {
        var rows = SelectRows(rowsByPath, paths);
        var jobProgress = new Lz4JobProgress();

        BatchItemStarted(paths);

        return Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var row in rows)
            {
                row.Progress = 0;
                row.Status = "준비 중";
            }

            return new Progress<ProgressInfo>(info =>
            {
                if (info.Percent < 0)
                    return;

                foreach (var row in rows)
                {
                    row.Progress = info.Percent;

                    if (!string.IsNullOrEmpty(info.Label))
                        row.Status = info.Label;
                }

                BatchItemProgress(paths, jobProgress.Combine(info.Percent));
            });
        });
    }

    private void FinishJob(Dictionary<string, List<GameFile>> rowsByPath, IReadOnlyList<string> paths, bool success)
    {
        var rows = SelectRows(rowsByPath, paths);

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var row in rows)
            {
                row.Progress = success ? 100 : 0;
                row.Status = success ? "완료" : "실패";
            }
        });

        BatchItemCompleted(paths, success);
    }

    private static List<GameFile> SelectRows(Dictionary<string, List<GameFile>> rowsByPath, IReadOnlyList<string> paths)
    {
        var rows = new List<GameFile>();

        foreach (var path in paths)
        {
            if (rowsByPath.TryGetValue(path, out var matched))
                rows.AddRange(matched);
        }

        return rows;
    }

    private bool ValidateInputs(IList<GameFile> gameFiles, out List<string> inputPaths, out string outputDir, out string errorMsg)
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

        path.OpenFolder();
    }
}