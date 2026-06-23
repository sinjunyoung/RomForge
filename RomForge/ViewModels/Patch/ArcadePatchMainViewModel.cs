using Common;
using Common.WPF.ViewModels;
using NSW.WPF.Services;
using RomForge.Core.Models.Patch;
using RomForge.Core.Services.Patch;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;

namespace RomForge.ViewModels.Patch;

public class ArcadePatchMainViewModel : ToolTabViewModel
{
    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ObservableCollection<ArcadeMatchItem> MatchItems { get; } = [];
    public ObservableCollection<PatchEntry> AllPatchEntries { get; } = [];
    public ObservableCollection<PatchEntry> UnmatchedPatches { get; } = [];
    public ObservableCollection<PatchPackage> AvailablePatchPackages { get; } = [];

    public bool HasPatchPackages => AvailablePatchPackages.Count > 0;

    private CancellationTokenSource? _analyzeCts;
    private CancellationTokenSource? _matchCts;
    private CancellationTokenSource? _runCts;
    private PatchPackage? _selectedPatchPackage;

    public PatchPackage? SelectedPatchPackage
    {
        get => _selectedPatchPackage;
        set
        {
            _selectedPatchPackage = value;
            OnPropertyChanged();
            _ = MatchItemsRebuildAsync();
        }
    }

    private string? _sourcePath;
    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            _sourcePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceLabel));

            if (value is not null)
                _ = AnalyzeAsync();
        }
    }

    private string? _patchPath;
    public string? PatchPath
    {
        get => _patchPath;
        set
        {
            _patchPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchLabel));

            if (value is not null)
                _ = AnalyzeAsync();
        }
    }

    private int _totalProgress;
    public int TotalProgress
    {
        get => _totalProgress;
        set { _totalProgress = value; OnPropertyChanged(); }
    }

    private string _progressSummary = string.Empty;
    public string ProgressSummary
    {
        get => _progressSummary;
        set { _progressSummary = value; OnPropertyChanged(); }
    }

    private string? _mismatchReason;
    public string? MismatchReason
    {
        get => _mismatchReason;
        set
        {
            _mismatchReason = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MismatchVisibility));
        }
    }

    public string SourceLabel => Path.GetFileName(SourcePath) ?? "원본 ZIP을 드래그하거나 클릭하세요";
    public string PatchLabel => Path.GetFileName(PatchPath) ?? "패치(IPS/폴더/ZIP)를 드래그하거나 클릭하세요";
    public Visibility HintVisibility => MatchItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MismatchVisibility => MismatchReason is not null ? Visibility.Visible : Visibility.Collapsed;

    public void Log(string message, LogLevel level)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
            LogEntries.Add(new LogEntry { Message = message, Level = level }));
    }

    public async Task RunAsync()
    {
        var matched = MatchItems.Where(x => x.IsMatched).ToList();

        if (matched.Count == 0 || SourcePath is null)
            return;

        if (MatchItems.Any(x => x.MismatchReason == "CRC 불일치"))
        {
            Log("CRC 불일치 항목이 있어 패치를 진행할 수 없습니다.", LogLevel.Error);
            return;
        }

        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        string outputDir = Path.Combine(Path.GetDirectoryName(SourcePath)!, "output");
        string outputZipPath = Path.Combine(outputDir, Path.GetFileName(SourcePath));

        Log($"패치 시작: {Path.GetFileName(SourcePath)}", LogLevel.Highlight);

        var itemsByEntryName = new Dictionary<string, ArcadeMatchItem>();
        var patchesByEntryName = new Dictionary<string, PatchEntry>();

        foreach (var item in matched)
        {
            var entryName = item.SourcePath.Split('|', 2)[1];
            itemsByEntryName[entryName] = item;
            patchesByEntryName[entryName] = item.PatchEntry!;
            item.Progress = 0;
        }

        var progressReporter = new Progress<EntryPatchProgress>(p =>
        {
            if (itemsByEntryName.TryGetValue(p.EntryName, out var item))
            {
                item.Progress = p.Percent;
                UpdateSummary();
                UpdateTotalProgress();
            }
        });

        try
        {
            await PatchService.ApplyPatchedZipAsync(SourcePath, outputZipPath, patchesByEntryName, progressReporter, ct);

            outputDir.OpenFolder();

            Log($"패치 완료: {SourcePath}", LogLevel.Ok);
        }
        catch (OperationCanceledException)
        {
            TryDeleteIncompleteOutput(outputZipPath);
            Log($"패치 취소: {Path.GetFileName(SourcePath)}", LogLevel.Error);
        }
        catch (Exception ex)
        {
            TryDeleteIncompleteOutput(outputZipPath);
            Log($"패치 실패: {ex.Message}", LogLevel.Error);
        }
    }

    public void Cancel()
    {
        _runCts?.Cancel();
    }

    private void TryDeleteIncompleteOutput(string outputZipPath)
    {
        try
        {
            if (File.Exists(outputZipPath))
                File.Delete(outputZipPath);
        }
        catch (Exception ex)
        {
            Log($"중단된 결과 파일 삭제 실패: {ex.Message} (수동으로 확인해주세요: {outputZipPath})", LogLevel.Error);
        }
    }

    public void ManualMatch(ArcadeMatchItem item, PatchEntry? patch)
    {
        if (patch is not null)
        {
            var previousOwner = MatchItems.FirstOrDefault(x => x != item && ReferenceEquals(x.PatchEntry, patch));

            if (previousOwner is not null)
            {
                previousOwner.PatchEntry = null;
                previousOwner.PatchFileName = null;
            }

            if (!AllPatchEntries.Any(p => ReferenceEquals(p, patch)))
                AllPatchEntries.Add(patch);

            item.MismatchReason = null;
        }

        item.PatchEntry = patch;
        item.PatchFileName = patch?.DisplayName;

        RefreshUnmatchedPatches();
        UpdateSummary();
    }

    public void UpdateTotalProgress()
    {
        var matchedItems = MatchItems.Where(x => x.IsMatched).ToList();

        if (matchedItems.Count == 0)
        {
            TotalProgress = 0;
            return;
        }

        TotalProgress = (int)matchedItems.Average(x => x.Progress);
    }

    public void UpdateSummary()
    {
        var matchedItems = MatchItems.Where(x => x.IsMatched).ToList();
        int completedCount = matchedItems.Count(x => x.Progress >= 100);
        ProgressSummary = $"{completedCount} / {matchedItems.Count} 완료";
    }

    public void Clear()
    {
        _analyzeCts?.Cancel();
        _matchCts?.Cancel();
        _runCts?.Cancel();

        SourcePath = null;
        PatchPath = null;
        MatchItems.Clear();
        AllPatchEntries.Clear();
        UnmatchedPatches.Clear();
        AvailablePatchPackages.Clear();
        LogEntries.Clear();
        SelectedPatchPackage = null;
        TotalProgress = 0;
        ProgressSummary = string.Empty;
        OnPropertyChanged(nameof(HintVisibility));
        OnPropertyChanged(nameof(HasPatchPackages));
    }

    private async Task AnalyzeAsync()
    {
        if (SourcePath is null || PatchPath is null)
            return;

        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _analyzeCts, cts);

        previous?.Cancel();
        previous?.Dispose();

        var token = cts.Token;
        var patchPath = PatchPath;

        try
        {
            List<PatchPackage> packages;
            try
            {
                packages = await Task.Run(() => BuildPatchPackages(patchPath, token), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            token.ThrowIfCancellationRequested();
            AvailablePatchPackages.Clear();

            foreach (var package in packages)
                AvailablePatchPackages.Add(package);

            OnPropertyChanged(nameof(HasPatchPackages));
            SelectedPatchPackage = AvailablePatchPackages.FirstOrDefault();
        }
        finally
        {
            if (ReferenceEquals(_analyzeCts, cts))
            {
                _analyzeCts = null;
                cts.Dispose();
            }
        }
    }

    private async Task MatchItemsRebuildAsync()
    {
        if (SourcePath is null || PatchPath is null)
            return;

        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _matchCts, cts);

        previous?.Cancel();
        previous?.Dispose();

        var token = cts.Token;
        var sourcePath = SourcePath;
        var patchPath = PatchPath;
        var package = SelectedPatchPackage;

        try
        {
            MatchPlan plan;
            try
            {
                plan = await Task.Run(() => BuildMatchPlan(sourcePath, patchPath, package, token), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            token.ThrowIfCancellationRequested();
            AllPatchEntries.Clear();

            foreach (var p in plan.PatchEntries)
                AllPatchEntries.Add(p);

            MatchItems.Clear();

            foreach (var r in plan.Results)
            {
                MatchItems.Add(new ArcadeMatchItem
                {
                    SourceFileName = r.SourceFileName,
                    SourcePath = r.FullPath,
                    PatchEntry = r.PatchEntry,
                    PatchFileName = r.PatchFileName,
                    MismatchReason = r.MismatchReason
                });
            }

            RefreshUnmatchedPatches();
            UpdateSummary();
            OnPropertyChanged(nameof(HintVisibility));
        }
        finally
        {
            if (ReferenceEquals(_matchCts, cts))
            {
                _matchCts = null;
                cts.Dispose();
            }
        }
    }

    private void RefreshUnmatchedPatches()
    {
        UnmatchedPatches.Clear();

        var used = MatchItems
            .Select(m => m.PatchEntry)
            .Where(p => p is not null)
            .Select(p => p!)
            .ToHashSet();

        foreach (var p in AllPatchEntries.Where(p => !used.Contains(p)))
            UnmatchedPatches.Add(p);
    }

    private sealed record PatchMatchResult(string SourceFileName, string FullPath, PatchEntry? PatchEntry, string? PatchFileName, string? MismatchReason);
    private sealed record MatchPlan(List<PatchEntry> PatchEntries, List<PatchMatchResult> Results);

    private static List<PatchPackage> BuildPatchPackages(string patchPath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        return [.. GetDatFiles(patchPath)
            .OrderBy(d => d.FileName)
            .Select(d =>
            {
                token.ThrowIfCancellationRequested();
                return PatchPackageService.ParseDatFile(d.FileName, d.Content);
            })];
    }

    private static MatchPlan BuildMatchPlan(string sourcePath, string patchPath, PatchPackage? package, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var sourceEntries = GetSourceEntries(sourcePath);
        var patchEntries = GetPatchEntries(patchPath);
        var results = new List<PatchMatchResult>(sourceEntries.Count);
        var allowedPatchNames = package?.Entries.Select(e => e.PatchBaseName).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var sortedPatches = patchEntries.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        var usedPatches = new HashSet<PatchEntry>();
        var extensionBuckets = new Dictionary<string, Queue<PatchEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in sortedPatches)
        {
            bool eligibleForExtensionMatch = package == null ||
                !allowedPatchNames.Contains(p.FileNameWithoutExtension, StringComparer.OrdinalIgnoreCase);

            if (!eligibleForExtensionMatch)
                continue;

            if (!extensionBuckets.TryGetValue(p.FileNameWithoutExtension, out var queue))
            {
                queue = new Queue<PatchEntry>();
                extensionBuckets[p.FileNameWithoutExtension] = queue;
            }

            queue.Enqueue(p);
        }

        var datCandidates = new LinkedList<PatchEntry>(sortedPatches);

        foreach (var (fileName, fullPath, crc) in sourceEntries)
        {
            token.ThrowIfCancellationRequested();

            PatchEntry? matched = null;
            string? mismatchReason = null;

            var datEntry = package?.Entries.FirstOrDefault(e => string.Equals(e.SourceFileName, fileName, StringComparison.OrdinalIgnoreCase));

            if (datEntry is not null)
            {
                if (string.Equals(crc, datEntry.Crc, StringComparison.OrdinalIgnoreCase))
                {
                    matched = FindAndRemoveDatMatch(datCandidates, usedPatches, datEntry.PatchBaseName);
                    mismatchReason = matched is null ? $"마스터 데이터에 등록된 패치({datEntry.PatchBaseName}.ips)를 찾을 수 없습니다." : "CRC 일치";
                }
                else
                    mismatchReason = "CRC 불일치";
            }
            else
            {
                var ext = Path.GetExtension(fileName).TrimStart('.').ToLower();
                matched = DequeueUnused(extensionBuckets, ext, usedPatches);
            }

            if (matched is not null)
                usedPatches.Add(matched);

            results.Add(new PatchMatchResult(fileName, fullPath, matched, matched?.DisplayName, mismatchReason));
        }

        return new MatchPlan(patchEntries, results);
    }

    private static PatchEntry? FindAndRemoveDatMatch(LinkedList<PatchEntry> candidates, HashSet<PatchEntry> usedPatches, string patchBaseName)
    {
        var node = candidates.First;

        while (node is not null)
        {
            var next = node.Next;

            if (usedPatches.Contains(node.Value))
                candidates.Remove(node);
            else if (node.Value.FileNameWithoutExtension.Contains(patchBaseName, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Remove(node);
                return node.Value;
            }

            node = next;
        }

        return null;
    }

    private static PatchEntry? DequeueUnused(Dictionary<string, Queue<PatchEntry>> buckets, string extension, HashSet<PatchEntry> usedPatches)
    {
        var key = buckets.Keys.FirstOrDefault(k => k.Contains(extension, StringComparison.OrdinalIgnoreCase));

        if (key is null || !buckets.TryGetValue(key, out var queue))
            return null;

        while (queue.Count > 0)
        {
            var candidate = queue.Dequeue();

            if (!usedPatches.Contains(candidate))
                return candidate;
        }

        return null;
    }

    private static List<(string FileName, string FullPath, string Crc)> GetSourceEntries(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);

        return [.. zip.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Select(e => (e.Name, $"{zipPath}|{e.FullName}", e.Crc32.ToString("x8")))];
    }

    private static List<PatchEntry> GetPatchEntries(string path)
    {
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            using var zip = ZipFile.OpenRead(path);

            return [.. zip.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.EndsWith(".ips", StringComparison.OrdinalIgnoreCase))
                .Select(e => new PatchEntry
                {
                    DisplayName = e.Name,
                    ZipPath = path,
                    EntryPath = e.FullName
                })];
        }

        if (File.Exists(path))
            return path.EndsWith(".ips", StringComparison.OrdinalIgnoreCase)
                ? [new PatchEntry { DisplayName = Path.GetFileName(path), EntryPath = path }] : [];

        if (Directory.Exists(path))
            return [.. Directory.GetFiles(path, "*.ips", SearchOption.AllDirectories)
                .Select(f => new PatchEntry { DisplayName = Path.GetFileName(f), EntryPath = f })];

        return [];
    }

    private static List<(string FileName, string Content)> GetDatFiles(string patchPath)
    {
        if (patchPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(patchPath))
        {
            using var zip = ZipFile.OpenRead(patchPath);

            return [.. zip.Entries
                .Where(e => e.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                .Select(e =>
                {
                    using var stream = e.Open();
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                    return (e.Name, reader.ReadToEnd());
                })];
        }

        if (Directory.Exists(patchPath))
            return [.. Directory.GetFiles(patchPath, "*.dat", SearchOption.AllDirectories)
                .Select(f => (Path.GetFileName(f), File.ReadAllText(f, Encoding.UTF8)))];

        return [];
    }
}