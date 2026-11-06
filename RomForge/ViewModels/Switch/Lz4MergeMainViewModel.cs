using Common.Models;
using Common.Services;
using NSW.Core;
using RomForge.Core;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels.Switch;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using Res = NSW.Properties.Resources;

namespace RomForge.ViewModels.Switch;

public class Lz4MergeMainViewModel : ProgressToolTabViewModel
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
                    file.Progress = 0;

                var results = await Task.Run(() => Lz4MergeService.RunAsync(inputPaths, outputDir, compressLevel, paths => CreateJobProgress(rowsByPath, paths), (paths, success) => FinishJob(rowsByPath, paths, success),
                    (msg, level) => AppendLog(msg, level), token), token);

                if (results.Count > 0)
                {
                    AppendLog(string.Format(Res.Main_Log_AllComplete, _totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);
                    AppendLog(Res.Main_Msg_Done);
                    MainViewModel.SetTaskbarProgress(100);
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
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);

                foreach (var file in gameFiles.Where(f => f.Progress < 100))
                    file.Progress = 0;

                await SoundPlayer.PlayCancelAsync();
            }
            catch (Exception ex)
            {
                AppendLog($"{Res.Log_Error}: {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
            }
            finally
            {
                CleanupTask();
            }
        }
    }

    private static Progress<ProgressInfo> CreateJobProgress(Dictionary<string, List<GameFile>> rowsByPath, IReadOnlyList<string> paths)
    {
        var rows = SelectRows(rowsByPath, paths);

        return Application.Current.Dispatcher.Invoke(() => new Progress<ProgressInfo>(info =>
        {
            foreach (var row in rows)
                row.Progress = info.Percent;

            MainViewModel.SetTaskbarProgress(info.Percent);
        }));
    }

    private static void FinishJob(Dictionary<string, List<GameFile>> rowsByPath, IReadOnlyList<string> paths, bool success)
    {
        var rows = SelectRows(rowsByPath, paths);

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var row in rows)
                row.Progress = success ? 100 : 0;

            if (!success)
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
        });
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