using Common.Localization;
using CHD.Core.Services;
using Common.Models;
using Common.Services;
using Patch.Core.Formats;
using Patch.Core.Formats.DCP.Services;
using RomForge.Core;
using RomForge.Core.Models.Compression;
using RomForge.Core.Models.Naming;
using RomForge.Core.Models.Patch;
using RomForge.Core.Services;
using RomForge.Core.Services.Compression;
using RomForge.Core.Services.Naming;
using RomForge.Core.Services.Patch;
using RomForge.Core.ViewModels.Patch;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class NormalPatchMainViewModel : ProgressToolTabViewModel, IPatchViewModel
{
    private CancellationTokenSource? _previewCts;
    private bool _patchAdded;
    private string? _cachedPatchVersion;
    private string? _cachedPatchDate;
    private string? _cachedPatchLanguage;
    private string? _lastProcessedPatchPath;

    public string? SourcePath
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(SourceLabel));
            OnPropertyChanged(nameof(HasSource));
            OnPropertyChanged(nameof(SourceHintVisible));
            CommandManager.InvalidateRequerySuggested();

            _lastProcessedPatchPath = null;

            TriggerPreviewUpdate();
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

    private string? NamingPatchPath => PatchPaths.Count > 0 ? PatchPaths[^1] : null;

    public bool AutoCompress
    {
        get => AppConfig.Instance.Patch.AutoCompress;
        set
        {
            AppConfig.Instance.Patch.AutoCompress = value;

            OnPropertyChanged(nameof(AutoCompress));
            TriggerPreviewUpdate();
        }
    }

    public bool NamingEnabled
    {
        get => AppConfig.Instance.Patch.NamingEnabled;
        set
        {
            AppConfig.Instance.Patch.NamingEnabled = value;

            OnPropertyChanged(nameof(NamingEnabled));
            TriggerPreviewUpdate();
        }
    }

    public string NamingFormat
    {
        get => AppConfig.Instance.Patch.NamingFormat;
        set
        {
            AppConfig.Instance.Patch.NamingFormat = value;

            OnPropertyChanged(nameof(NamingFormat));
            TriggerPreviewUpdate();
        }
    }

    public static IReadOnlyList<NamingTokenDefinition> NamingTokens => NamingTokenCatalog.Patch;

    public string? NamingPreview { get; private set { SetProperty(ref field, value); } }

    public Func<IReadOnlyList<ArchiveCandidate>, Task<string?>>? RequestSourceSelectionAsync { get; set; }

    public string SourceLabel => Path.GetFileName(SourcePath) ?? Loc.S.HintDragOrClickSourceFile;

    public override string? HelpPageKey => "patch-general";

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
                TriggerPreviewUpdate();
            }
        };

        AppConfig.Instance.Patch.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PatchConfig.AutoCompress))
            {
                OnPropertyChanged(nameof(AutoCompress));
                TriggerPreviewUpdate();
            }

            if (e.PropertyName == nameof(PatchConfig.NamingEnabled))
            {
                OnPropertyChanged(nameof(NamingEnabled));
                TriggerPreviewUpdate();
            }

            if (e.PropertyName == nameof(PatchConfig.NamingFormat))
            {
                OnPropertyChanged(nameof(NamingFormat));
                TriggerPreviewUpdate();
            }
        };

        Loc.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Loc.Current))
                OnPropertyChanged(nameof(SourceLabel));
        };

        TriggerPreviewUpdate();
    }

    public void AddPatchSlot()
    {
        if (!CanAddPatch)
            return;

        PatchSlots.Add(CreateSlot());

        _patchAdded = true;

        for (int i = 0; i < PatchSlots.Count; i++)
            PatchSlots[i].Title = Loc.S.LabelPatchNumber(i + 1);

        OnPropertyChanged(nameof(CanAddPatch));
        OnPropertyChanged(nameof(HintsVisible));
        OnPropertyChanged(nameof(IsMultiPatch));
        OnPropertyChanged(nameof(SourceHintVisible));
        TriggerPreviewUpdate();
    }

    private PatchSlotViewModel CreateSlot()
    {
        var slot = new PatchSlotViewModel();

        slot.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PatchSlotViewModel.FilePath))
            {
                _lastProcessedPatchPath = null;

                TriggerPreviewUpdate();
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
        PatchSlots[0].Title = Common.Localization.Loc.Instance.Current.HeaderPatch;
        _patchAdded = false;

        OnPropertyChanged(nameof(CanAddPatch));
        OnPropertyChanged(nameof(HintsVisible));
        OnPropertyChanged(nameof(IsMultiPatch));
        OnPropertyChanged(nameof(SourceHintVisible));

        _lastProcessedPatchPath = null;

        TriggerPreviewUpdate();
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

        string namingPatchPath = patchPaths[patchPaths.Count - 1];

        CancellationTokenSource = new CancellationTokenSource();

        var ct = CancellationTokenSource.Token;
        string outputDir = ResolveOutputDir(SourcePath);
        string? extractDir = null;
        string? outputPath = null;
        var orchestrator = new Orchestrator(AppendLog, BuildProgressReporter(), AutoCompress, AppConfig.Instance.WiiGC.CompressLevel);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Chain.Validate(patchPaths);
            Directory.CreateDirectory(outputDir);

            string? actualSourcePath = SourcePath;

            if (SourceArchiveExtractor.IsArchivePath(SourcePath))
            {
                AppendLog(Loc.S.LogCheckingSourceArchive(Path.GetFileName(SourcePath)), LogLevel.Highlight);

                extractDir = Path.Combine(outputDir, "_src_" + Path.GetFileNameWithoutExtension(SourcePath)); Directory.CreateDirectory(extractDir);

                var extractResult = await SourceArchiveExtractor.AnalyzeAndExtractAsync(SourcePath, extractDir, BuildProgressReporter(), ct);

                if (extractResult.NeedsSelection)
                {
                    AppendLog(Loc.S.LogArchiveMultipleCandidatesFound(extractResult.Candidates.Count), LogLevel.Highlight);

                    if (RequestSourceSelectionAsync is null)
                        throw new InvalidOperationException(Loc.S.ErrArchiveCandidatesNoSelector);

                    string entryKey = await RequestSourceSelectionAsync(extractResult.Candidates) ?? throw new OperationCanceledException();

                    actualSourcePath = await SourceArchiveExtractor.ExtractCandidateAsync(SourcePath, extractDir, entryKey, BuildProgressReporter(), ct);
                }
                else
                    actualSourcePath = extractResult.ResolvedPath!;

                AppendLog(Loc.S.LogSourceArchiveExtracted(Path.GetFileName(actualSourcePath)), LogLevel.Ok);
            }

            extractDir ??= Path.Combine(outputDir, "_src_" + Path.GetFileNameWithoutExtension(actualSourcePath));

            if ((Path.GetExtension(actualSourcePath).Equals(".chd", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(actualSourcePath).Equals(".rvz", StringComparison.OrdinalIgnoreCase)) && patchPaths.All(XdeltaAppHeaderReader.TargetsCompressedContainer))
            {
                string directOutputName = VersionInfoExtractor.ApplySuffix(Path.GetFileName(actualSourcePath), namingPatchPath, AppConfig.Instance.Patch.NamingEnabled, AppConfig.Instance.Patch.NamingFormat, Common.Localization.Loc.S.EnglishName);
                string directOutputPath = Utils.GetUniqueFilePath(Path.Combine(outputDir, directOutputName));

                AppendLog(Loc.S.LogTryingPatchOnArchive, LogLevel.Highlight);

                try
                {
                    await Chain.ApplyAsync(actualSourcePath, patchPaths, directOutputPath, outputDir, AppendLog, BuildProgressReporter(), ct);
                    stopwatch.Stop();

                    outputPath = directOutputPath;

                    AppendLog(Loc.S.LogPatchDoneWithTime(Path.GetFileName(outputPath), stopwatch.Elapsed.ToString("mm\\:ss")), LogLevel.Ok);
                    outputDir.OpenFolder();

                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("XD3_INVALID_INPUT"))
                {
                    AppendLog(Loc.S.LogArchiveMismatchRetry, LogLevel.Highlight);

                    if (File.Exists(directOutputPath))
                        File.Delete(directOutputPath);
                }
            }

            var (resolvedSourcePath, detected) = await CompressedSourceDecompressor.ResolveAsync(actualSourcePath, extractDir, AppendLog, BuildProgressReporter(), ct);

            actualSourcePath = resolvedSourcePath;

            bool sourceIsTemporary = Path.GetFullPath(Path.GetDirectoryName(actualSourcePath)!)
                .Equals(Path.GetFullPath(extractDir), StringComparison.OrdinalIgnoreCase);
            string outputFileName = VersionInfoExtractor.ApplySuffix(ResolveOutputBaseFileName(actualSourcePath), namingPatchPath, AppConfig.Instance.Patch.NamingEnabled, AppConfig.Instance.Patch.NamingFormat, Common.Localization.Loc.S.EnglishName);

            outputPath = Path.Combine(outputDir, outputFileName);
            outputPath = Utils.GetUniqueFilePath(outputPath);

            AppendLog(Loc.S.LogPatchStartName(Path.GetFileName(actualSourcePath)), LogLevel.Highlight);
            await orchestrator.PatchAsync(actualSourcePath, patchPaths, detected, outputDir, outputPath, sourceIsTemporary, ct);
            stopwatch.Stop();
            AppendLog(Loc.S.LogPatchDoneWithTime(Path.GetFileName(outputPath), stopwatch.Elapsed.ToString("mm\\:ss")), LogLevel.Ok);
            await SoundPlayer.PlayCompleteAsync();
            outputDir.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            AppendLog(Loc.S.LogPatchCancelledName(SourcePath), LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();            

            if (outputPath is not null)
                orchestrator.Cleanup(outputPath);
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogPatchFailedMsg(ex.Message), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();            
            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);

            if (outputPath is not null)
                orchestrator.Cleanup(outputPath);
        }
        finally
        {
            CleanupTask();

            if (extractDir is not null && Directory.Exists(extractDir))
            {
                try
                {
                    Directory.Delete(extractDir, true);
                    AppendLog(Loc.S.LogTempExtractCleaned, LogLevel.Ok);
                }
                catch (Exception ex)
                {
                    AppendLog(Loc.S.LogTempExtractCleanupFailed(ex.Message), LogLevel.Error);
                    await SoundPlayer.PlayErrorAsync();
                }
            }
        }
    }

    private static string ResolveOutputBaseFileName(string? actualSourcePath)
    {
        if (actualSourcePath == null)
            throw new NullReferenceException();

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

    private void TriggerPreviewUpdate()
    {
        _previewCts?.Cancel();

        _previewCts = new CancellationTokenSource();

        var token = _previewCts.Token;

        Task.Delay(100, token).ContinueWith(t =>
        {
            if (t.IsCanceled)
                return;

            string baseFileName = ResolvePreviewBaseFileName();

            if (!NamingEnabled)
            {
                NamingPreview = baseFileName;
                return;
            }

            string? namingPatchPath = NamingPatchPath;

            if (namingPatchPath != _lastProcessedPatchPath)
            {
                _lastProcessedPatchPath = namingPatchPath;

                var (v, d, l) = VersionInfoExtractor.GetPatchMetadata(namingPatchPath);

                _cachedPatchVersion = v;
                _cachedPatchDate = d;
                _cachedPatchLanguage = l;
            }

            string version = _cachedPatchVersion ?? "1.0";
            string date = _cachedPatchDate ?? DateTime.Now.ToString("yyMMdd");
            string? language = _cachedPatchLanguage;
            string result = VersionInfoExtractor.ApplyFormat(baseFileName, version, date, language, NamingFormat, Common.Localization.Loc.S.EnglishName);

            NamingPreview = result;

        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private string ResolvePreviewBaseFileName()
    {
        if (SourcePath is null)
            return Loc.S.LabelSourceBinFileName;

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

    public bool CanRun() => !string.IsNullOrEmpty(SourcePath) && PatchPaths.Count > 0;

    public void Clear()
    {
        CancellationTokenSource?.Cancel();

        SourcePath = null;

        ResetPatchSlots();
        CleanupTask();
        LogEntries.Clear();
    }
}