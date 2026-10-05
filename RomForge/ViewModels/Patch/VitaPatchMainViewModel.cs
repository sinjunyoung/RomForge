using Common;
using Common.WPF.ViewModels;
using NSW.WPF.Services;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Services.Patch;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using Vita.Core.Models;
using Vita.Core.Services;

namespace RomForge.ViewModels.Patch;

public class VitaPatchMainViewModel : ToolTabViewModel, IPatchViewModel
{
    private readonly Stopwatch _totalSw = new();
    private CancellationTokenSource? _cts;

    private string? _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
    private bool _buildEmu = true;
    private bool _buildRetail;
    private bool _mergePatchIntoGame = false;
    private int _progressPct;
    private string _progressLabel = string.Empty;
    private string _progressPercent = string.Empty;
    private string _progressTime = string.Empty;
    private string _progressSpeed = string.Empty;
    private bool _isSyncing;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ObservableCollection<VitaSourceRowViewModel> SourceRows { get; } = [];

    public string? OutputPath
    {
        get => _outputPath;
        set
        {
            _outputPath = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputHintVisibility));

            AppConfig.Instance.OutputFolders.VitaOutputPath = value;
        }
    }

    public Visibility OutputHintVisibility => string.IsNullOrWhiteSpace(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EntriesHintVisibility => SourceRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool BuildEmu
    {
        get => _buildEmu;
        set { _buildEmu = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public bool BuildRetail
    {
        get => _buildRetail;
        set { _buildRetail = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public int ProgressPct
    {
        get => _progressPct;
        set 
        { 
            _progressPct = value; 
            OnPropertyChanged();
            MainViewModel.SetTaskbarProgress(_progressPct);
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

    public bool MergePatchIntoGame
    {
        get => _mergePatchIntoGame;
        set { _mergePatchIntoGame = value; OnPropertyChanged(); }
    }

    public string? SourcePath => SourceRows.FirstOrDefault()?.Path;

    public ICommand RunCommand { get; }
    public ICommand RemoveRowCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand RemoveAllCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    private VitaSourceRowViewModel? _selectedRow;
    public VitaSourceRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set { _selectedRow = value; OnPropertyChanged(); }
    }

    public VitaPatchMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.VitaOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.VitaOutputPath;

        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsLocked && CanRun());        
        RemoveRowCommand = new RelayCommand(o => RemoveRow(o as VitaSourceRowViewModel ?? SelectedRow));
        RemoveSelectedCommand = new RelayCommand(_ => { if (SelectedRow != null) RemoveRow(SelectedRow); }, _ => SelectedRow != null);
        RemoveAllCommand = new RelayCommand(_ => SourceRows.Clear(), _ => SourceRows.Count > 0);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
        CancelCommand = new RelayCommand(_ => Cancel());

        SourceRows.CollectionChanged += SourceRows_CollectionChanged;
    }

    private void SourceRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (VitaSourceRowViewModel row in e.NewItems)
                row.PropertyChanged += Row_PropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (VitaSourceRowViewModel row in e.OldItems)
                row.PropertyChanged -= Row_PropertyChanged;
        }

        OnPropertyChanged(nameof(EntriesHintVisibility));
        RecomputeLicenseEditability();
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncing)
            return;

        if (sender is not VitaSourceRowViewModel changedRow)
            return;

        _isSyncing = true;

        try
        {
            if (e.PropertyName == nameof(VitaSourceRowViewModel.PatchPath))
            {
                foreach (var row in SourceRows)
                {
                    if (row != changedRow && !string.Equals(row.PatchPath, changedRow.PatchPath))
                        row.PatchPath = changedRow.PatchPath;
                }
            }
            else if (e.PropertyName == nameof(VitaSourceRowViewModel.License))
            {
                if (changedRow.Category != VitaContentCategory.Addcont)
                {
                    foreach (var row in SourceRows)
                    {
                        if (row.IsPkg && row.Category != VitaContentCategory.Addcont && row != changedRow && !string.Equals(row.License, changedRow.License))
                            row.License = changedRow.License;
                    }
                }
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void BrowseOutput()
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "출력 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.SelectedPath;
    }

    public bool CanRun()
    {
        if (string.IsNullOrWhiteSpace(OutputPath) || (!BuildEmu && !BuildRetail))
            return false;

        if (SourceRows.Count == 0 || SourceRows.Any(r => string.IsNullOrWhiteSpace(r.PatchPath)))
            return false;

        bool everyTitleHasApp = SourceRows
            .GroupBy(r => r.TitleId, StringComparer.OrdinalIgnoreCase)
            .All(g => g.Any(r => r.Category == VitaContentCategory.App));

        return everyTitleHasApp;
    }

    public void AddSourceFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        bool isDirectory = Directory.Exists(path);
        bool isPkg = path.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase);
        bool isZip = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        if (!isDirectory && !isPkg && !isZip)
            return;

        if (isPkg)
        {
            VitaSourceRowViewModel row;

            try
            {
                row = VitaSourceRowViewModel.FromPkg(path);
            }
            catch (Exception ex)
            {
                Log($"{Path.GetFileName(path)}: 분석 실패 - {ex.Message}", LogLevel.Error);
                return;
            }

            ApplyDefaultsAndLog(row, path);
            AddOrReplaceRow(row);
        }
        else
        {
            List<VitaSourceRowViewModel> discoveredRows;

            try
            {
                discoveredRows = VitaSourceRowViewModel.DiscoverFromContainer(path);
            }
            catch (Exception ex)
            {
                Log($"{Path.GetFileName(path)}: 분석 실패 - {ex.Message}", LogLevel.Error);
                return;
            }

            foreach (var row in discoveredRows)
            {
                ApplyDefaultsAndLog(row, null);
                AddOrReplaceRow(row);
            }
        }
    }

    private void ApplyDefaultsAndLog(VitaSourceRowViewModel row, string? pkgPathForLicenseTxt)
    {
        if (row.IsPkg && pkgPathForLicenseTxt != null)
        {
            string? dir = Path.GetDirectoryName(pkgPathForLicenseTxt);

            if (!string.IsNullOrEmpty(dir))
            {
                string txtPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(pkgPathForLicenseTxt) + ".txt");

                if (File.Exists(txtPath))
                {
                    try
                    {
                        string txtContent = File.ReadAllText(txtPath).Trim();

                        if (!string.IsNullOrEmpty(txtContent))
                            row.License = txtContent;
                    }
                    catch
                    {
                    }
                }
            }

            if (row.Category != VitaContentCategory.Addcont)
            {
                var existingAppOrPatch = SourceRows.FirstOrDefault(r => r.IsPkg && r.Category != VitaContentCategory.Addcont && !string.IsNullOrEmpty(r.License));

                if (existingAppOrPatch != null && string.IsNullOrEmpty(row.License))
                    row.License = existingAppOrPatch.License;
            }
        }

        var existingAny = SourceRows.FirstOrDefault();

        if (existingAny != null && !string.IsNullOrEmpty(existingAny.PatchPath))
            row.PatchPath = existingAny.PatchPath;

        _ = row.LoadMetadataAsync();
    }

    private void AddOrReplaceRow(VitaSourceRowViewModel newRow)
    {
        if (newRow.Category is VitaContentCategory.App or VitaContentCategory.Patch)
        {
            var existing = SourceRows.FirstOrDefault(r => r.Category == newRow.Category);

            if (existing != null)
            {
                Log($"{existing.FileName} -> {newRow.FileName}(으)로 대체됨 (기존 {newRow.Category})");
                SourceRows.Remove(existing);
            }
        }
        else
        {
            var duplicate = SourceRows.FirstOrDefault(r => r.Category == VitaContentCategory.Addcont && string.Equals(r.TitleId, newRow.TitleId, StringComparison.OrdinalIgnoreCase) && string.Equals(r.ContentIdSuffix, newRow.ContentIdSuffix, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                Log($"{duplicate.FileName} -> {newRow.FileName}(으)로 대체됨 (같은 DLC)");
                SourceRows.Remove(duplicate);
            }
        }

        SourceRows.Insert(ComputeInsertIndex(newRow.Category, newRow.ContentIdSuffix), newRow);
    }

    private int ComputeInsertIndex(VitaContentCategory category, string? contentIdSuffix)
    {
        if (category == VitaContentCategory.App)
            return 0;

        if (category == VitaContentCategory.Patch)
            return SourceRows.Any(r => r.Category == VitaContentCategory.App) ? 1 : 0;

        int index = SourceRows.Count(r => r.Category is VitaContentCategory.App or VitaContentCategory.Patch);

        foreach (var row in SourceRows)
        {
            if (row.Category != VitaContentCategory.Addcont)
                continue;

            if (string.Compare(contentIdSuffix, row.ContentIdSuffix, StringComparison.OrdinalIgnoreCase) < 0)
                break;

            index++;
        }

        return index;
    }

    private void RecomputeLicenseEditability()
    {
        foreach (var row in SourceRows)
        {
            if (!row.IsPkg)
            {
                row.IsLicenseEditable = false;
                continue;
            }

            if (row.Category == VitaContentCategory.Addcont)
            {
                row.IsLicenseEditable = true;
                continue;
            }

            var appRow = SourceRows.FirstOrDefault(r => r.Category == VitaContentCategory.App && string.Equals(r.TitleId, row.TitleId, StringComparison.OrdinalIgnoreCase));

            row.IsLicenseEditable = appRow == null || appRow.IsPkg;

            if (!row.IsLicenseEditable && !string.IsNullOrEmpty(row.License))
                row.License = string.Empty;
        }
    }

    public void RemoveRow(VitaSourceRowViewModel? row)
    {
        if (row != null)
        {
            row.PropertyChanged -= Row_PropertyChanged;
            SourceRows.Remove(row);
        }
    }

    public async Task RunAsync()
    {
        string? zipFileName = null;

        _totalSw.Restart();

        using (BeginWork())
        {
            try
            {
                _cts = new CancellationTokenSource();

                string baseName = SourceRows.First(r => r.Category == VitaContentCategory.App).TitleId;
                string defaultPatchPath = SourceRows.FirstOrDefault(r => !string.IsNullOrEmpty(r.PatchPath))?.PatchPath ?? string.Empty;
                var entries = SourceRows.Select(r => r.ToBatchEntry()).ToList();
                VitaOutputTarget target;
                string label;

                if (BuildEmu)
                {
                    target = VitaOutputTarget.Emu;
                    label = "에뮬";
                }
                else if (BuildRetail)
                {
                    target = VitaOutputTarget.Retail;
                    label = "실기";
                }
                else
                    return;

                string suffix = MergePatchIntoGame ? $"_{label.ToLower()}_merged.zip" : $"_{label.ToLower()}.zip";
                string fileName = PatchVersionInfoExtractor.ApplySuffix($"{baseName}{suffix}", defaultPatchPath);
                zipFileName = Utils.GetUniqueFilePath(Path.Combine(OutputPath!, fileName));

                if (MergePatchIntoGame)
                {
                    Log($"{label}용 게임+패치 병합 중...", LogLevel.Highlight);

                    var result = await VitaPatchOutputBuilder.BuildMergedFromEntriesAsync(entries, defaultPatchPath, zipFileName, target, Log, BuildProgressReporter(), _cts.Token);

                    Log($"{label}용 완료: 총 {result.TotalFiles}개 파일 (패치 {result.PatchedSuccessfully}/{result.PatchCandidates}개 적용) -> {zipFileName}", LogLevel.Ok);
                }
                else
                {
                    Log($"{label}용 패치 생성 중...", LogLevel.Highlight);

                    var result = await VitaPatchOnlyBuilder.BuildFromEntriesAsync(entries, defaultPatchPath, zipFileName, target, Log, BuildProgressReporter(), _cts.Token);

                    Log($"{label}용 완료: 매칭 {result.MatchedCandidates}개 중 {result.PatchedSuccessfully}개 성공 -> {zipFileName}", LogLevel.Ok);
                }

                Log($"전체 완료 ({_totalSw.Elapsed:mm\\:ss})", LogLevel.Ok);
                OutputPath?.OpenFolder();
            }
            catch (OperationCanceledException)
            {
                Log("작업이 취소되었습니다.", LogLevel.Error);                
                SafeDeleteFile(zipFileName);
                MainViewModel.SetTaskbarProgress(0);
            }
            catch (Exception ex)
            {
                Log($"오류: {ex.Message}", LogLevel.Error);
                SafeDeleteFile(zipFileName);
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                CleanupProgress();
            }
        }
    }

    private static void SafeDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch { }
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

    private void CleanupProgress()
    {
        ProgressPct = 0;
        ProgressLabel = string.Empty;
        ProgressPercent = string.Empty;
        ProgressTime = string.Empty;
        ProgressSpeed = string.Empty;
    }

    public void Clear()
    {
        _cts?.Cancel();

        SourceRows.Clear();

        CleanupProgress();

        LogEntries.Clear();
    }

    public void Cancel() => _cts?.Cancel();

    public void Log(string msg, LogLevel level = LogLevel.Info) => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
}