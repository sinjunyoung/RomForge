using Common;
using Common.WPF.ViewModels;
using NSW.WPF.Services;
using Patch.Core.Services.PC98;
using RomForge.Core.Models;
using RomForge.Core.Services.Patch;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class Pc98PatchMainViewModel : ToolTabViewModel, IPatchViewModel
{
    private CancellationTokenSource? _runCts;
    private string? _sourcePath;
    private string? _patchPath;
    private int _progressPct;
    private string _progressLabel = string.Empty;
    private string _progressPercent = "0%";

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

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
        }
    }

    public string SourceLabel => Path.GetFileName(SourcePath) ?? "원본 HDI를 드래그하거나 클릭하세요";

    public string PatchLabel => Path.GetFileName(PatchPath) ?? "한글패치 폴더/ZIP을 드래그하거나 클릭하세요";

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

    public void Log(string message, LogLevel level) =>
        Application.Current?.Dispatcher?.Invoke(() => LogEntries.Add(new LogEntry { Message = message, Level = level }));

    public async Task RunAsync()
    {
        if (SourcePath is null || PatchPath is null)
            return;

        _runCts = new CancellationTokenSource();

        var ct = _runCts.Token;
        string outputDir = Path.Combine(Path.GetDirectoryName(SourcePath)!, "output");
        string outputPath = Utils.GetUniqueFilePath(Path.Combine(outputDir, Path.GetFileName(SourcePath)));

        Log($"패치 시작: {Path.GetFileName(SourcePath)}", LogLevel.Highlight);

        var progress = new Progress<ProgressInfo>(p =>
        {
            ProgressPct = p.Percent;
            ProgressLabel = p.Label;
            ProgressPercent = $"{p.Percent}%";
        });

        try
        {
            Directory.CreateDirectory(outputDir);

            var result = await Pc98PatchService.ApplyAsync(SourcePath, PatchPath, outputPath, Log, progress, ct);

            if (result.MissingFiles.Count > 0)
                Log($"HDI에서 찾지 못한 파일 {result.MissingFiles.Count}개는 건너뛰었습니다.", LogLevel.Error);

            Log($"패치 완료: {result.AppliedCount}개 파일 적용 → {Path.GetFileName(outputPath)}", LogLevel.Ok);

            outputDir.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            TryDeleteIncompleteOutput(outputPath);
            Log($"패치 취소: {Path.GetFileName(SourcePath)}", LogLevel.Error);
        }
        catch (Exception ex)
        {
            TryDeleteIncompleteOutput(outputPath);
            Log($"패치 실패: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            ProgressPct = 0;
            ProgressLabel = string.Empty;
            ProgressPercent = "0%";
        }
    }

    public void Cancel() => _runCts?.Cancel();

    public void Clear()
    {
        _runCts?.Cancel();

        SourcePath = null;
        PatchPath = null;

        ProgressPct = 0;
        ProgressLabel = string.Empty;
        ProgressPercent = "0%";

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
            Log($"중단된 결과 파일 삭제 실패: {ex.Message} (수동으로 확인해주세요: {outputPath})", LogLevel.Error);
        }
    }
}
