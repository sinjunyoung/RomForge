using CHD.Core.Services;
using Common;
using Common.WPF.ViewModels;
using NSW.WPF.Services;
using Patch.Core.Formats;
using Patch.Core.Formats.DCP.Services;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Models.Compression;
using RomForge.Core.Models.Patch;
using RomForge.Core.Services.Compression;
using RomForge.Core.Services.Patch;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class NormalPatchMainViewModel : ToolTabViewModel, IPatchViewModel
{
    private CancellationTokenSource? _runCts;
    private string? _sourcePath;
    private bool _patchAdded;
    private int _progressPct;
    private string _progressLabel = string.Empty;
    private string _progressPercent = "0%";
    private string _progressTime = string.Empty;
    private string _progressSpeed = string.Empty;

    public System.Collections.ObjectModel.ObservableCollection<LogEntry> LogEntries { get; } = [];

    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            _sourcePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceLabel));
            OnPropertyChanged(nameof(NamingPreview));
            OnPropertyChanged(nameof(HasSource));
            OnPropertyChanged(nameof(SourceHintVisible));
            CommandManager.InvalidateRequerySuggested();

            _ = SourceHash.StartAsync(value);
        }
    }

    public SourceHashViewModel SourceHash { get; } = new();

    public bool HasSource => !string.IsNullOrEmpty(SourcePath);

    public bool SourceHintVisible => HintsVisible && !HasSource;

    public const int MaxPatchCount = 5;

    public System.Collections.ObjectModel.ObservableCollection<PatchSlotViewModel> PatchSlots { get; } = [];

    public bool CanAddPatch => PatchSlots.Count < MaxPatchCount;

    public bool HintsVisible => !_patchAdded;

    public bool IsMultiPatch => _patchAdded;

    public IReadOnlyList<string> PatchPaths => [.. PatchSlots.Select(s => s.FilePath).OfType<string>().Where(p => p.Length > 0)];

    private string? NamingPatchPath => PatchPaths.LastOrDefault();

    public bool AutoCompress
    {
        get => AppConfig.Instance.Patch.AutoCompress;
        set
        {
            AppConfig.Instance.Patch.AutoCompress = value;
            OnPropertyChanged(nameof(AutoCompress));
            OnPropertyChanged(nameof(NamingPreview));
        }
    }

    public bool NamingEnabled
    {
        get => AppConfig.Instance.Patch.NamingEnabled;
        set
        {
            AppConfig.Instance.Patch.NamingEnabled = value;
            OnPropertyChanged(nameof(NamingEnabled));
            OnPropertyChanged(nameof(NamingPreview));
        }
    }

    public string NamingFormat
    {
        get => AppConfig.Instance.Patch.NamingFormat;
        set
        {
            AppConfig.Instance.Patch.NamingFormat = value;
            OnPropertyChanged(nameof(NamingFormat));
            OnPropertyChanged(nameof(NamingPreview));
        }
    }

    public string NamingPreview
    {
        get
        {
            string baseFileName = ResolvePreviewBaseFileName();

            if (!NamingEnabled)
                return baseFileName;

            string? version;
            string date;

            string? namingPatchPath = NamingPatchPath;

            if (namingPatchPath is not null && File.Exists(namingPatchPath))
            {
                (version, string? extractedDate) = PatchVersionInfoExtractor.Extract(Path.GetFileName(namingPatchPath));
                date = extractedDate ?? File.GetLastWriteTime(namingPatchPath).ToString("yyMMdd");
            }
            else
            {
                version = "1.0";
                date = DateTime.Now.ToString("yyMMdd");
            }

            return PatchVersionInfoExtractor.ApplyFormat(baseFileName, version, date, NamingFormat);
        }
    }

    private string ResolvePreviewBaseFileName()
    {
        if (SourcePath is null)
            return "원본.bin";

        if (!File.Exists(SourcePath) || SourceArchiveExtractor.IsArchivePath(SourcePath))
            return Path.GetFileName(SourcePath);

        string ext = Path.GetExtension(SourcePath);

        if (ext.Equals(".chd", StringComparison.OrdinalIgnoreCase) || ext.Equals(".rvz", StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(SourcePath);

        string baseFileName = ResolveOutputBaseFileName(SourcePath);

        if (AutoCompress)
        {
            try
            {
                var detected = FormatDetector.Detect(SourcePath);

                if (detected.Direction == ConvertDirection.Compress && !string.IsNullOrEmpty(detected.OutputExtension))
                    baseFileName = Path.ChangeExtension(baseFileName, detected.OutputExtension);
            }
            catch { }
        }

        return baseFileName;
    }

    public Func<IReadOnlyList<ArchiveCandidate>, Task<string?>>? RequestSourceSelectionAsync { get; set; }

    public string SourceLabel => Path.GetFileName(SourcePath) ?? "원본 파일을 드래그하거나 클릭하세요";

    public int ProgressPct
    {
        get => _progressPct;
        set 
        { 
            _progressPct = value;
            MainViewModel.SetTaskbarProgress(_progressPct);
            OnPropertyChanged();            
        }
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

    public NormalPatchMainViewModel()
    {
        PatchSlots.Add(CreateSlot());

        AppConfig.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppConfig.Patch))
            {
                OnPropertyChanged(nameof(AutoCompress));
                OnPropertyChanged(nameof(NamingEnabled));
                OnPropertyChanged(nameof(NamingFormat));
                OnPropertyChanged(nameof(NamingPreview));
            }
        };

        AppConfig.Instance.Patch.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PatchConfig.AutoCompress))
            {
                OnPropertyChanged(nameof(AutoCompress));
                OnPropertyChanged(nameof(NamingPreview));
            }

            if (e.PropertyName == nameof(PatchConfig.NamingEnabled))
            {
                OnPropertyChanged(nameof(NamingEnabled));
                OnPropertyChanged(nameof(NamingPreview));
            }

            if (e.PropertyName == nameof(PatchConfig.NamingFormat))
            {
                OnPropertyChanged(nameof(NamingFormat));
                OnPropertyChanged(nameof(NamingPreview));
            }
        };
    }

    public void AddPatchSlot()
    {
        if (!CanAddPatch)
            return;

        PatchSlots.Add(CreateSlot());

        _patchAdded = true;

        for (int i = 0; i < PatchSlots.Count; i++)
            PatchSlots[i].Title = $"패치 {i + 1}";

        OnPropertyChanged(nameof(CanAddPatch));
        OnPropertyChanged(nameof(HintsVisible));
        OnPropertyChanged(nameof(IsMultiPatch));
        OnPropertyChanged(nameof(SourceHintVisible));
    }

    private PatchSlotViewModel CreateSlot()
    {
        var slot = new PatchSlotViewModel();

        slot.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PatchSlotViewModel.FilePath))
            {
                OnPropertyChanged(nameof(NamingPreview));
                CommandManager.InvalidateRequerySuggested();
            }
        };

        return slot;
    }

    private void ResetPatchSlots()
    {
        while (PatchSlots.Count > 1)
            PatchSlots.RemoveAt(PatchSlots.Count - 1);

        PatchSlots[0].FilePath = null;
        PatchSlots[0].Title = "패치";

        _patchAdded = false;

        OnPropertyChanged(nameof(CanAddPatch));
        OnPropertyChanged(nameof(HintsVisible));
        OnPropertyChanged(nameof(IsMultiPatch));
        OnPropertyChanged(nameof(SourceHintVisible));
    }

    public void Log(string message, LogLevel level)
    {
        Application.Current?.Dispatcher?.Invoke(() => LogEntries.Add(new LogEntry { Message = message, Level = level }));
    }

    private static string ResolveOutputDir(string sourcePath)
    {
        var patch = AppConfig.Instance.Patch;

        if (patch.UseNormalPatchCustomOutputPath && !string.IsNullOrWhiteSpace(patch.OutputPath))
            return patch.OutputPath;

        return Path.Combine(Path.GetDirectoryName(sourcePath)!, "output");
    }

    public async Task RunAsync()
    {
        var patchPaths = PatchPaths;

        if (SourcePath is null || patchPaths.Count == 0)
            return;

        SourceHash.Cancel();

        string namingPatchPath = patchPaths.Last();

        _runCts = new CancellationTokenSource();

        var ct = _runCts.Token;
        string outputDir = ResolveOutputDir(SourcePath);
        string? extractDir = null;
        string? outputPath = null;
        var orchestrator = new PatchOrchestrator(Log, BuildProgressReporter(), AutoCompress, AppConfig.Instance.WiiGC.CompressLevel);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            PatchChain.Validate(patchPaths);

            Directory.CreateDirectory(outputDir);

            string actualSourcePath = SourcePath;

            if (SourceArchiveExtractor.IsArchivePath(SourcePath))
            {
                Log($"원본 압축 확인 중: {Path.GetFileName(SourcePath)}", LogLevel.Highlight);

                extractDir = Path.Combine(outputDir, "_src_" + Path.GetFileNameWithoutExtension(SourcePath)); Directory.CreateDirectory(extractDir);

                var extractResult = await SourceArchiveExtractor.AnalyzeAndExtractAsync(SourcePath, extractDir, BuildProgressReporter(), ct);

                if (extractResult.NeedsSelection)
                {
                    Log($"압축 안에 패치 대상 후보가 {extractResult.Candidates.Count}개 있습니다. 선택이 필요합니다.", LogLevel.Highlight);

                    if (RequestSourceSelectionAsync is null)
                        throw new InvalidOperationException("압축 안에 후보가 여러 개인데 선택 UI가 연결되어 있지 않습니다.");

                    string entryKey = await RequestSourceSelectionAsync(extractResult.Candidates) ?? throw new OperationCanceledException();

                    actualSourcePath = await SourceArchiveExtractor.ExtractCandidateAsync(SourcePath, extractDir, entryKey, BuildProgressReporter(), ct);
                }
                else
                    actualSourcePath = extractResult.ResolvedPath!;

                Log($"원본 압축 해제 완료: {Path.GetFileName(actualSourcePath)}", LogLevel.Ok);
            }

            extractDir ??= Path.Combine(outputDir, "_src_" + Path.GetFileNameWithoutExtension(actualSourcePath));

            if ((Path.GetExtension(actualSourcePath).Equals(".chd", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(actualSourcePath).Equals(".rvz", StringComparison.OrdinalIgnoreCase)) && patchPaths.All(XdeltaAppHeaderReader.TargetsCompressedContainer))
            {
                string directOutputName = PatchVersionInfoExtractor.ApplySuffix(Path.GetFileName(actualSourcePath), namingPatchPath, AppConfig.Instance.Patch.NamingEnabled, AppConfig.Instance.Patch.NamingFormat);
                string directOutputPath = Utils.GetUniqueFilePath(Path.Combine(outputDir, directOutputName));

                Log("압축된 원본에 바로 패치를 시도합니다...", LogLevel.Highlight);

                try
                {
                    await PatchChain.ApplyAsync(actualSourcePath, patchPaths, directOutputPath, outputDir, Log, BuildProgressReporter(), ct);
                    stopwatch.Stop();

                    outputPath = directOutputPath;

                    Log($"패치 완료: {Path.GetFileName(outputPath)} ({stopwatch.Elapsed:mm\\:ss})", LogLevel.Ok);
                    outputDir.OpenFolder();

                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("XD3_INVALID_INPUT") || ex.Message.Contains("미스매치"))
                {
                    Log("압축된 원본과 패치가 일치하지 않아 압축을 해제한 뒤 다시 시도합니다.", LogLevel.Highlight);

                    if (File.Exists(directOutputPath))
                        File.Delete(directOutputPath);
                }
            }

            var (resolvedSourcePath, detected) = await CompressedSourceDecompressor.ResolveAsync(actualSourcePath, extractDir, Log, BuildProgressReporter(), ct);

            actualSourcePath = resolvedSourcePath;

            bool sourceIsTemporary = Path.GetFullPath(Path.GetDirectoryName(actualSourcePath)!)
                .Equals(Path.GetFullPath(extractDir), StringComparison.OrdinalIgnoreCase);
            string outputFileName = PatchVersionInfoExtractor.ApplySuffix(ResolveOutputBaseFileName(actualSourcePath), namingPatchPath, AppConfig.Instance.Patch.NamingEnabled, AppConfig.Instance.Patch.NamingFormat);

            outputPath = Path.Combine(outputDir, outputFileName);
            outputPath = Utils.GetUniqueFilePath(outputPath);

            Log($"패치 시작: {Path.GetFileName(actualSourcePath)}", LogLevel.Highlight);

            await orchestrator.PatchAsync(actualSourcePath, patchPaths, detected, outputDir, outputPath, sourceIsTemporary, ct);
            stopwatch.Stop();
            Log($"패치 완료: {Path.GetFileName(outputPath)} ({stopwatch.Elapsed:mm\\:ss})", LogLevel.Ok);
            outputDir.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            Log($"패치 취소: {SourcePath}", LogLevel.Error);
            CleanupTask();

            if (outputPath is not null)
                orchestrator.Cleanup(outputPath);
        }
        catch (Exception ex)
        {
            Log($"패치 실패: {ex.Message}", LogLevel.Error);
            CleanupTask();
            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);

            if (outputPath is not null)
                orchestrator.Cleanup(outputPath);
        }
        finally
        {
            if (extractDir is not null && Directory.Exists(extractDir))
            {
                try
                {
                    Directory.Delete(extractDir, true);
                    Log("원본 압축 해제 임시 파일 정리 완료", LogLevel.Ok);
                }
                catch (Exception ex)
                {
                    Log($"압축 해제 임시 파일 정리 실패: {ex.Message}", LogLevel.Error);
                }
            }
        }
    }

    private Progress<ProgressInfo> BuildProgressReporter() =>
        new(info =>
        {
            ProgressPct = info.Percent;
            ProgressLabel = info.Label;
            ProgressPercent = $"{info.Percent}%";
            ProgressTime = info.TimeInfo;
            ProgressSpeed = info.Speed;
        });

    private void CleanupTask()
    {
        ProgressPct = 0;
        ProgressLabel = string.Empty;
        ProgressPercent = "0%";
        ProgressTime = string.Empty;
        ProgressSpeed = string.Empty;
    }

    private static string ResolveOutputBaseFileName(string actualSourcePath)
    {
        string sourceMainFileName = Path.GetFileName(actualSourcePath);
        string ext = Path.GetExtension(actualSourcePath);
        var dir = Path.GetDirectoryName(actualSourcePath);

        if (dir is not null)
        {
            foreach (var candidate in Directory.GetFiles(dir, "*.cue"))
            {
                if (ConversionSource.ParseBinsFromCue(candidate).Any(b => string.Equals(Path.GetFileName(b), sourceMainFileName, StringComparison.OrdinalIgnoreCase)))
                    return Path.GetFileNameWithoutExtension(candidate) + ext;
            }

            foreach (var candidate in Directory.GetFiles(dir, "*.gdi"))
            {
                try
                {
                    if (GdiFile.Parse(candidate).Tracks.Any(t => string.Equals(t.FileName, sourceMainFileName, StringComparison.OrdinalIgnoreCase)))
                        return Path.GetFileNameWithoutExtension(candidate) + ext;
                }
                catch { }
            }
        }

        return sourceMainFileName;
    }

    public bool CanRun() => !string.IsNullOrEmpty(SourcePath) && PatchPaths.Count > 0;

    public void Cancel() => _runCts?.Cancel();

    public void Clear()
    {
        _runCts?.Cancel();

        SourcePath = null;
        ResetPatchSlots();
        AutoCompress = false;

        CleanupTask();

        LogEntries.Clear();
    }
}