using Common;
using NSW.Core;
using NSW.WPF.Services;
using NSW.WPF.ViewModels;
using RomForge.Core;
using RomForge.Core.Models.Switch;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using RomForge.Core.UI.Command;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.ViewModels.Switch;

public class MergeMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();

    private string _outputPath = string.Empty;
    private OutputFormat _outputFormat = OutputFormat.NSP;
    private MergeMode _mergeMode = MergeMode.ByTitleId;

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            _outputPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputHintVisibility));
            AppConfig.Instance.OutputFolders.SwitchMergeOutputPath = value;
        }
    }

    public OutputFormat OutputFormat
    {
        get => _outputFormat;
        set { _outputFormat = value; OnPropertyChanged(); }
    }

    public MergeMode MergeMode
    {
        get => _mergeMode;
        set { _mergeMode = value; OnPropertyChanged(); }
    }

    public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public ICommand OpenWorkSpaceCommand { get; }

    public MergeMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.SwitchMergeOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.SwitchMergeOutputPath;
        OpenWorkSpaceCommand = new RelayCommand(_ => ExecuteOpenWorkSpace());
    }

    public async Task MergeAsync(IList<GameFile> gameFiles)
    {
        if (!ValidateMergeInputs(gameFiles, out var inputPaths, out var outputDir, out string errorMsg))
        {
            AppendLog(errorMsg, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        _totalSw.Restart();
        using (BeginWork())
        {
            try
            {
                CancellationTokenSource = new CancellationTokenSource();

                var progress = BuildProgressReporter();
                bool useCompression = OutputFormat is OutputFormat.NSZ or OutputFormat.XCZ;
                bool asXci = OutputFormat is OutputFormat.XCI or OutputFormat.XCZ;
                var config = AppConfig.Instance.Switch;
                var keySet = KeySetProvider.Instance.KeySet.Clone();
                var results = new List<string>();
                bool mergeAll = MergeMode == MergeMode.AllInOne;
                void Log(string msg, LogLevel level, string id) => AppendLog(msg, level);

                List<string> output =
                    asXci
                    ? (mergeAll
                        ? await XciMergeService.RunMergeSingle(inputPaths, outputDir, useCompression, config.CompressLevel, config.UseBlockMode, config.VerifyCompress, config.ForceKeyGen0, keySet.Clone(), progress, Log, CancellationTokenSource.Token)
                        : await XciMergeService.RunMergeAll(inputPaths, outputDir, useCompression, config.CompressLevel, config.UseBlockMode, config.VerifyCompress, config.ForceKeyGen0, keySet.Clone(), progress, Log, CancellationTokenSource.Token))
                    : (mergeAll
                        ? await NspMergeService.RunMergeSingle(inputPaths, outputDir, useCompression, config.CompressLevel, config.UseBlockMode, config.VerifyCompress, config.ForceKeyGen0, keySet.Clone(), progress, Log, CancellationTokenSource.Token)
                        : await NspMergeService.RunMergeAll(inputPaths, outputDir, useCompression, config.CompressLevel, config.UseBlockMode, config.VerifyCompress, config.ForceKeyGen0, keySet.Clone(), progress, Log, CancellationTokenSource.Token));

                results.AddRange(output);

                if (results?.Count > 0)
                {
                    AppendLog(string.Format(Res.Main_Log_AllComplete, _totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);
                    AppendLog(Res.Main_Msg_Done);
                    await SoundPlayer.PlayCompleteAsync();
                    outputDir.OpenFolder();
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog($"병합이 취소되었습니다.", LogLevel.Error);
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
}