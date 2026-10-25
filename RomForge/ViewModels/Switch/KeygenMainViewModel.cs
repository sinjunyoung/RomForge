using Common;
using NSW.Core.ViewModels;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using System.Diagnostics;
using System.IO;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.ViewModels.Switch;

public class KeygenMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();

    public KeygenMainViewModel() { }

    public async Task ExecuteStartWorkAsync(IList<GameFile> gameFiles)
    {
        if (IsLocked) 
            return;

        if (!ValidateAndGetInputs(gameFiles, out var inputPaths, out string errorMsg))
        {
            AppendLog(errorMsg, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (gameFiles.Any(f => f.IsKeyMissing))
            ProgressLabel = Res.Main_Log_Recalculating;

        _totalSw.Restart();

        using (BeginWork())
        {
            try
            {
                CancellationTokenSource = new CancellationTokenSource();
                var progress = BuildProgressReporter();
                void Log(string msg, LogLevel level, string id) => AppendLog(msg, level);
                var results = await Task.Run(async () => await NspRecryptService.Recrypt(inputPaths, true, progress, Log, CancellationTokenSource.Token), CancellationTokenSource.Token);

                if (results != null && results.Count > 0)
                {
                    AppendLog(string.Format(Res.Main_Log_AllComplete, _totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);
                    await SoundPlayer.PlayCompleteAsync();
                    AppendLog(Res.Main_Msg_Done);
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog($"작업이 취소되었습니다.", LogLevel.Error);
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
}