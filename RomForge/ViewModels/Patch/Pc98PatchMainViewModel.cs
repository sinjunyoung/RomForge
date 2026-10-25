using Common;
using NSW.Core.Services;
using RomForge.Core.Services;
using RomForge.Core.Services.Patch;
using System.IO;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class Pc98PatchMainViewModel : ProgressToolTabViewModel, IPatchViewModel
{
    private string? _sourcePath;
    private string? _patchPath;

    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            _sourcePath = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceLabel));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string? PatchPath
    {
        get => _patchPath;
        set
        {
            _patchPath = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchLabel));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string SourceLabel => Path.GetFileName(SourcePath) ?? "원본 HDI를 드래그하거나 클릭하세요";

    public string PatchLabel => Path.GetFileName(PatchPath) ?? "패치 폴더 또는 ZIP를 드래그하거나 클릭하세요";


    public async Task RunAsync()
    {
        if (SourcePath is null || PatchPath is null)
            return;

        CancellationTokenSource = new CancellationTokenSource();

        var ct = CancellationTokenSource.Token;
        string outputDir = Path.Combine(Path.GetDirectoryName(SourcePath)!, "output");
        string outputPath = Utils.GetUniqueFilePath(Path.Combine(outputDir, Path.GetFileName(SourcePath)));

        AppendLog($"패치 시작: {Path.GetFileName(SourcePath)}", LogLevel.Highlight);

        try
        {
            Directory.CreateDirectory(outputDir);

            var result = await global::Patch.Core.Formats.PC98.PatchService.ApplyAsync(SourcePath, PatchPath, outputPath, AppendLog, BuildProgressReporter(), ct);

            if (result.MissingFiles.Count > 0)
                AppendLog($"HDI에서 찾지 못한 파일 {result.MissingFiles.Count}개는 건너뛰었습니다.", LogLevel.Error);

            AppendLog($"패치 완료: {result.AppliedCount}개 파일 적용 → {Path.GetFileName(outputPath)}", LogLevel.Ok);
            await SoundPlayer.PlayCompleteAsync();
            outputDir.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            TryDeleteIncompleteOutput(outputPath);
            AppendLog($"패치 취소: {Path.GetFileName(SourcePath)}", LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            MainViewModel.SetTaskbarProgress(0);
        }
        catch (Exception ex)
        {
            TryDeleteIncompleteOutput(outputPath);
            AppendLog($"패치 실패: {ex.Message}", LogLevel.Error);
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
            AppendLog($"중단된 결과 파일 삭제 실패: {ex.Message} (수동으로 확인해주세요: {outputPath})", LogLevel.Error);
        }
    }
}