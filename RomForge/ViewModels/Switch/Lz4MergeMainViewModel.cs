using Common;
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

                var progress = BuildProgressReporter();
                var token = CancellationTokenSource.Token;
                int compressLevel = AppConfig.Instance.Switch.Lz4CompressLevel;
                var results = await Task.Run(() => Lz4MergeService.RunAsync(inputPaths, outputDir, compressLevel, progress, (msg, level) => AppendLog(msg, level), token), token);

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
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
            }
            catch (Exception ex)
            {
                AppendLog($"{Res.Log_Error}: {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
            }
            finally
            {
                CleanupTask();
            }
        }
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