using Common.Models;
using Common.Localization;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using RomForge.Core.ViewModels.Switch;
using System.Diagnostics;
using System.IO;

namespace RomForge.ViewModels.Switch;

public class KeygenMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();

    public override string? HelpPageKey => "switch-version-ignore";

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
            ProgressLabel = Loc.S.MainLogRecalculating;

        _totalSw.Restart();

        using (BeginWork())
        {
            try
            {
                CancellationTokenSource = new CancellationTokenSource();
                var progress = BuildProgressReporter();
                void Log(string msg, LogLevel level, string? id) => AppendLog(msg, level);
                var results = await Task.Run(async () => await NspRecryptService.Recrypt(inputPaths, true, progress, Log, CancellationTokenSource.Token), CancellationTokenSource.Token);

                if (results != null && results.Count > 0)
                {
                    AppendLog(Loc.S.MainLogAllComplete( _totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);
                    await SoundPlayer.PlayCompleteAsync();
                    AppendLog(Loc.S.MainMsgDone);
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog(Loc.S.LogJobCancelled, LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
            }
            catch (Exception ex)
            {
                AppendLog($"{Loc.S.LogError}: {ex.Message}", LogLevel.Error);
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
            errorMsg = Loc.S.MainErrNoFiles;
            return false;
        }

        if (gameFiles.Any(f => f.IsKeyMissing))
        {
            errorMsg = Loc.S.MainErrNoKeys;
            return false;
        }

        string[] allowedExts = [".nsp", ".nsz", ".xci", ".xcz"];
        inputPaths = [.. gameFiles
            .Where(f => allowedExts.Contains(Path.GetExtension(f.FilePath).ToLowerInvariant()))
            .Select(f => f.FilePath)];

        if (inputPaths.Count == 0)
        {
            errorMsg = Loc.S.MainErrNoFiles;
            return false;
        }

        return true;
    }
}