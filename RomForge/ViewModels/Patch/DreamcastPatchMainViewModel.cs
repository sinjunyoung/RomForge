using CHD.Core.Services;
using Common;
using Common.WPF.ViewModels;
using NSW.WPF.Services;
using Patch.Core.Formats.DCP.Services;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Services.Patch;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class DreamcastPatchMainViewModel : ToolTabViewModel, IPatchViewModel
{
    private CancellationTokenSource? _runCts;

    private string? _sourcePath;
    private string? _patchPath;
    private int _progressPct;
    private string _progressLabel = string.Empty;
    private string _progressPercent = "0%";
    private string _progressTime = string.Empty;
    private string _progressSpeed = string.Empty;

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

    public bool AutoCompress
    {
        get => AppConfig.Instance.Patch.AutoCompress;
        set
        {
            AppConfig.Instance.Patch.AutoCompress = value;
            OnPropertyChanged(nameof(AutoCompress));
        }
    }

    public string SourceLabel => Path.GetFileName(SourcePath) ?? "원본 GDI를 드래그하거나 클릭하세요";

    public string PatchLabel => Path.GetFileName(PatchPath) ?? "DCP 패치를 드래그하거나 클릭하세요";

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

    public DreamcastPatchMainViewModel()
    {
        AppConfig.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppConfig.Patch))
                OnPropertyChanged(nameof(AutoCompress));
        };

        AppConfig.Instance.Patch.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PatchConfig.AutoCompress))
                OnPropertyChanged(nameof(AutoCompress));
        };
    }

    public void Log(string message, LogLevel level)
    {
        Application.Current?.Dispatcher?.Invoke(() => LogEntries.Add(new LogEntry { Message = message, Level = level }));
    }

    public async Task RunAsync()
    {
        if (SourcePath is null || PatchPath is null)
            return;

        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        string sourceDir = Path.GetDirectoryName(SourcePath)!;
        string outputDir = Path.Combine(sourceDir, "output", Path.GetFileNameWithoutExtension(SourcePath));

        Log($"드림캐스트 패치 시작: {Path.GetFileName(SourcePath)}", LogLevel.Highlight);

        try
        {
            Directory.CreateDirectory(outputDir);

            await DcpGdRomApplier.ApplyAsync(SourcePath, PatchPath, outputDir, (p, msg) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ProgressPct = AutoCompress ? (int)(p * 50) : (int)(p * 100);
                    ProgressLabel = msg;
                });
            }, ct);

            ProgressPct = AutoCompress ? 50 : 100;
            ProgressLabel = "패치 완료";
            Log($"패치 완료: {outputDir}", LogLevel.Highlight);

            string newGdiPath = Directory.GetFiles(outputDir, "*.gdi").First();

            if (AutoCompress)
            {
                ProgressLabel = "CHD 변환 중...";
                Log("CHD 변환 시작", LogLevel.Highlight);

                FileConverter converter = new(AppConfig.Instance.Chdman.Compression);
                converter.LogMessage += (_, e) => Log(e.Message, e.Level);                

                var chdProgress = new Progress<ProgressInfo>(p =>
                {
                    ProgressPct = 50 + (p.Percent / 2);
                    ProgressLabel = p.Label;
                    
                });

                ProgressReporter reporter = new(Path.GetFileName(SourcePath), string.Empty, 0, chdProgress);
                
                var chdResult = await converter.ConvertFileAsync(newGdiPath, chdProgress, ct);

                if (!chdResult.Success)
                    throw new Exception($"CHD 변환 실패: {chdResult.Message}");

                Log($"CHD 변환 완료", LogLevel.Highlight);
            }

            ProgressPct = 100;
            ProgressLabel = "완료";

            outputDir.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            CleanupTask();
            Log($"패치 취소: {SourcePath}", LogLevel.Error);

            DeleteOutputDirectory(outputDir);
        }
        catch (Exception ex)
        {
            Log($"패치 실패: {ex.Message}", LogLevel.Error);
            CleanupTask();
            DeleteOutputDirectory(outputDir);
        }
    }

    private static void DeleteOutputDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }

    public void Cancel() => _runCts?.Cancel();

    private void CleanupTask()
    {
        ProgressPct = 0;
        ProgressLabel = string.Empty;
        ProgressPercent = "0%";
        ProgressTime = string.Empty;
        ProgressSpeed = string.Empty;
    }

    public void Clear()
    {
        _runCts?.Cancel();

        SourcePath = null;
        PatchPath = null;
        AutoCompress = false;

        CleanupTask();

        LogEntries.Clear();
    }
}