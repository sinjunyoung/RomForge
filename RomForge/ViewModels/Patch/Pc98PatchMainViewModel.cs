using Common.Localization;
using Common.Models;
using Common.Services;
using RomForge.Core.Services;
using RomForge.Core.Services.Patch;
using System.IO;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class Pc98PatchMainViewModel : ProgressToolTabViewModel, IPatchViewModel
{
    public string? SourcePath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(SourceLabel)); CommandManager.InvalidateRequerySuggested(); } }

    public string? PatchPath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(PatchLabel)); CommandManager.InvalidateRequerySuggested(); } }

    public string SourceLabel => Path.GetFileName(SourcePath) ?? Loc.S.HintDragOrClickSourceHdi;

    public string PatchLabel => Path.GetFileName(PatchPath) ?? Loc.S.HintDragOrClickPatchFolderZip;

    public Pc98PatchMainViewModel()
    {
        Loc.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Loc.Current))
            {
                OnPropertyChanged(nameof(SourceLabel));
                OnPropertyChanged(nameof(PatchLabel));
            }
        };
    }

    public async Task RunAsync()
    {
        if (SourcePath is null || PatchPath is null)
            return;

        CancellationTokenSource = new CancellationTokenSource();

        var ct = CancellationTokenSource.Token;
        string outputDir = Path.Combine(Path.GetDirectoryName(SourcePath)!, "output");
        string outputPath = Utils.GetUniqueFilePath(Path.Combine(outputDir, Path.GetFileName(SourcePath)));

        AppendLog(Loc.S.LogPatchStartName(Path.GetFileName(SourcePath)), LogLevel.Highlight);

        try
        {
            Directory.CreateDirectory(outputDir);

            var result = await global::Patch.Core.Formats.PC98.PatchService.ApplyAsync(SourcePath, PatchPath, outputPath, AppendLog, BuildProgressReporter(), ct);

            if (result.MissingFiles.Count > 0)
                AppendLog(Loc.S.LogPc98MissingFilesSkipped(result.MissingFiles.Count), LogLevel.Error);

            AppendLog(Loc.S.LogPc98PatchDone(result.AppliedCount, Path.GetFileName(outputPath)), LogLevel.Ok);
            await SoundPlayer.PlayCompleteAsync();
            outputDir.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            TryDeleteIncompleteOutput(outputPath);
            AppendLog(Loc.S.LogPatchCancelledName(Path.GetFileName(SourcePath)), LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            MainViewModel.SetTaskbarProgress(0);
        }
        catch (Exception ex)
        {
            TryDeleteIncompleteOutput(outputPath);
            AppendLog(Loc.S.LogPatchFailedMsg(ex.Message), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
        }
        finally
        {
            CleanupTask();
        }
    }

    public bool CanRun() => !string.IsNullOrEmpty(SourcePath) && !string.IsNullOrEmpty(PatchPath);

    public void Clear()
    {
        CancellationTokenSource?.Cancel();

        SourcePath = null;
        PatchPath = null;

        CleanupTask();
        LogEntries.Clear();
    }

    private void TryDeleteIncompleteOutput(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogStaleResultDeleteFailed(ex.Message, outputPath), LogLevel.Error);
        }
    }
}