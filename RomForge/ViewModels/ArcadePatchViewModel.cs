using Common.WPF.ViewModels;
using RomForge.Core.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace RomForge.ViewModels;

public class ArcadePatchViewModel : ToolTabViewModel
{
    public ObservableCollection<ArcadeMatchItem> MatchItems { get; } = [];
    public List<PatchEntry> UnmatchedPatches { get; private set; } = [];

    private string? _sourcePath;
    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            _sourcePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceLabel));
            if (value is not null) Analyze();
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
            if (value is not null) Analyze();
        }
    }

    public string SourceLabel => SourcePath ?? "원본 ZIP을 드래그하거나 클릭하세요";
    public string PatchLabel => PatchPath ?? "패치(IPS/폴더/ZIP)를 드래그하거나 클릭하세요";
    public Visibility HintVisibility => MatchItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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

    public void ManualMatch(ArcadeMatchItem item, PatchEntry patch)
    {
        item.PatchEntry = patch;
        item.PatchFileName = patch.DisplayName;
        UpdateSummary();
    }

    public void UpdateTotalProgress()
    {
        if (MatchItems.Count == 0) { TotalProgress = 0; return; }
        TotalProgress = (int)MatchItems.Average(x => x.Progress);
    }

    public void UpdateSummary()
    {
        int matched = MatchItems.Count(x => x.IsMatched);
        ProgressSummary = $"{matched} / {MatchItems.Count} 매칭";
    }

    public void Clear()
    {
        SourcePath = null;
        PatchPath = null;
        MatchItems.Clear();
        UnmatchedPatches = [];
        TotalProgress = 0;
        ProgressSummary = string.Empty;
        OnPropertyChanged(nameof(HintVisibility));
    }

    private void Analyze()
    {
        if (SourcePath is null || PatchPath is null) return;

        MatchItems.Clear();

        var sourceEntries = GetSourceEntries(SourcePath);
        var patchEntries = GetPatchEntries(PatchPath);
        var usedPatches = new HashSet<PatchEntry>();

        foreach (var (fileName, fullPath) in sourceEntries)
        {
            var ext = Path.GetExtension(fileName).TrimStart('.').ToLower();

            var matched = patchEntries
                .Where(p => !usedPatches.Contains(p) &&
                    p.FileNameWithoutExtension.Contains(ext, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.DisplayName)
                .FirstOrDefault();

            if (matched is not null)
                usedPatches.Add(matched);

            MatchItems.Add(new ArcadeMatchItem
            {
                SourceFileName = fileName,
                SourcePath = fullPath,
                PatchEntry = matched,
                PatchFileName = matched?.DisplayName,
            });
        }

        UnmatchedPatches = patchEntries.Where(p => !usedPatches.Contains(p)).ToList();

        UpdateSummary();
        OnPropertyChanged(nameof(HintVisibility));
    }

    private static List<(string fileName, string fullPath)> GetSourceEntries(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return [.. zip.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Select(e => (e.Name, $"{zipPath}|{e.FullName}"))];
    }

    private static List<PatchEntry> GetPatchEntries(string path)
    {
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            using var zip = ZipFile.OpenRead(path);
            return [.. zip.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .Select(e => new PatchEntry
                {
                    DisplayName = e.Name,
                    ZipPath = path,
                    EntryPath = e.FullName
                })];
        }

        if (File.Exists(path))
            return [new PatchEntry { DisplayName = Path.GetFileName(path), EntryPath = path }];

        if (Directory.Exists(path))
            return [.. Directory.GetFiles(path)
                .Select(f => new PatchEntry { DisplayName = Path.GetFileName(f), EntryPath = f })];

        return [];
    }
}