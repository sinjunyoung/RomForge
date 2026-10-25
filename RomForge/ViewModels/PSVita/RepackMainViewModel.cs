using Common;
using RomForge.Core;
using RomForge.Core.Services;
using RomForge.Core.Services.Patch;
using RomForge.Core.Services.PSVita;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels.PSVita;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Vita.Core.Models;
using Vita.Core.Services;

namespace RomForge.ViewModels.PSVita;

public class RepackMainViewModel : ProgressToolTabViewModel
{
    private const string UnpackedFolderName = "unpacked";

    private readonly Stopwatch _totalSw = new();
    private BuildMode? _currentMode;
    private string? _pendingZipPath;
    private int _patchLogVersion;

    private string? _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
    private bool _buildEmu = true;
    private bool _buildRetail;
    private bool _mergePatchIntoGame = false;
    private bool _isSyncing;
    private SourceRowViewModel? _selectedRow;

    public ObservableCollection<SourceRowViewModel> SourceRows { get; } = [];

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
        set
        {
            _buildEmu = value;

            OnPropertyChanged();
        }
    }

    public bool BuildRetail
    {
        get => _buildRetail;
        set
        {
            _buildRetail = value;

            OnPropertyChanged();
        }
    }

    public bool MergePatchIntoGame
    {
        get => _mergePatchIntoGame;
        set
        {
            _mergePatchIntoGame = value;

            OnPropertyChanged();
        }
    }

    public SourceRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set { _selectedRow = value; OnPropertyChanged(); }
    }

    public bool IsUnpackRunning => IsLocked && _currentMode == BuildMode.UnpackOnly;

    public bool IsRebuildRunning => IsLocked && _currentMode == BuildMode.RebuildOnly;

    public bool IsFullRunning => IsLocked && _currentMode == BuildMode.FullProcess;

    public bool UnpackEnabled => !IsLocked || _currentMode == BuildMode.UnpackOnly;

    public bool RebuildEnabled => !IsLocked || _currentMode == BuildMode.RebuildOnly;

    public bool StartEnabled => !IsLocked || _currentMode == BuildMode.FullProcess;

    private string UnpackedRoot => Path.Combine(OutputPath!, UnpackedFolderName);

    public ICommand RemoveSelectedCommand { get; }
    public ICommand RemoveAllCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    public RepackMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.VitaOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.VitaOutputPath;

        RemoveSelectedCommand = new RelayCommand(_ => RemoveRow(SelectedRow), _ => SelectedRow != null);
        RemoveAllCommand = new RelayCommand(_ => SourceRows.Clear(), _ => SourceRows.Count > 0);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
        CancelCommand = new RelayCommand(_ => Cancel());

        SourceRows.CollectionChanged += SourceRows_CollectionChanged;

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsLocked))
                NotifyButtonStates();
        };

        _ = LoadExistingUnpackedAsync();
    }

    private async Task LoadExistingUnpackedAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputPath) || SourceRows.Count > 0)
            return;

        string root = UnpackedRoot;

        if (!Directory.Exists(root))
            return;

        var titles = await Task.Run(() => UnpackedScanner.Scan(root));

        if (titles.Count == 0 || SourceRows.Count > 0)
            return;

        foreach (var title in titles)
            SourceRows.Add(SourceRowViewModel.FromUnpacked(title));

        AppendLog($"언팩 폴더에서 {titles.Count}개 타이틀을 불러왔습니다.");
    }

    private async Task LogPatchInfoAsync(string? patchPath)
    {
        int version = ++_patchLogVersion;

        if (string.IsNullOrWhiteSpace(patchPath))
            return;

        try
        {
            var (total, patchFiles) = await Task.Run(() => PatchInspector.Inspect(patchPath));

            if (version == _patchLogVersion)
                AppendLog($"패치 정보를 읽었습니다: {total}개 파일 (패치 {patchFiles}개, 교체 {total - patchFiles}개)");
        }
        catch (Exception ex)
        {
            if (version == _patchLogVersion)
                AppendLog($"패치를 읽을 수 없습니다: {ex.Message}", LogLevel.Error);
        }
    }

    private void SourceRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (SourceRowViewModel row in e.NewItems)
                row.PropertyChanged += Row_PropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (SourceRowViewModel row in e.OldItems)
                row.PropertyChanged -= Row_PropertyChanged;
        }

        OnPropertyChanged(nameof(EntriesHintVisibility));
        RecomputeLicenseEditability();
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncing)
            return;

        if (sender is not SourceRowViewModel changedRow)
            return;

        _isSyncing = true;

        try
        {
            if (e.PropertyName == nameof(SourceRowViewModel.PatchPath))
            {
                foreach (var row in SourceRows)
                {
                    if (row != changedRow && !string.Equals(row.PatchPath, changedRow.PatchPath))
                        row.PatchPath = changedRow.PatchPath;
                }

                _ = LogPatchInfoAsync(changedRow.PatchPath);
            }
            else if (e.PropertyName == nameof(SourceRowViewModel.License))
            {
                if (changedRow.Category != ContentCategory.Addcont)
                {
                    foreach (var row in SourceRows)
                    {
                        if (row.IsPkg && row.Category != ContentCategory.Addcont && row != changedRow && !string.Equals(row.License, changedRow.License))
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

        if (dlg.ShowDialog() != true)
            return;

        OutputPath = dlg.SelectedPath;

        _ = LoadExistingUnpackedAsync();
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
            SourceRowViewModel row;

            try
            {
                row = SourceRowViewModel.FromPkg(path);
            }
            catch (Exception ex)
            {
                AppendLog($"{Path.GetFileName(path)}: 분석 실패 - {ex.Message}", LogLevel.Error);
                return;
            }

            ApplyDefaultsAndLog(row, path);
            AddOrReplaceRow(row);
        }
        else
        {
            List<SourceRowViewModel> discoveredRows;

            try
            {
                discoveredRows = SourceRowViewModel.DiscoverFromContainer(path);
            }
            catch (Exception ex)
            {
                AppendLog($"{Path.GetFileName(path)}: 분석 실패 - {ex.Message}", LogLevel.Error);
                return;
            }

            foreach (var row in discoveredRows)
            {
                ApplyDefaultsAndLog(row, null);
                AddOrReplaceRow(row);
            }
        }
    }

    private void ApplyDefaultsAndLog(SourceRowViewModel row, string? pkgPath)
    {
        if (row.IsPkg && pkgPath != null)
        {
            string? dir = Path.GetDirectoryName(pkgPath);

            if (!string.IsNullOrEmpty(dir))
            {
                string txtPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(pkgPath) + ".txt");

                if (File.Exists(txtPath))
                {
                    try
                    {
                        string txtContent = File.ReadAllText(txtPath).Trim();

                        if (!string.IsNullOrEmpty(txtContent))
                            row.License = txtContent;
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(row.License))
            {
                var group = NPSService.Instance.GetGroup(row.TitleId);

                row.License = row.Category switch
                {
                    ContentCategory.App => group?.App?.ZRif,
                    ContentCategory.Addcont => group?.GetDlcLicense(row.ContentIdSuffix),
                    _ => row.License
                };
            }

            if (row.Category != ContentCategory.Addcont)
            {
                var existingAppOrPatch = SourceRows.FirstOrDefault(r => r.IsPkg && r.Category != ContentCategory.Addcont && !string.IsNullOrEmpty(r.License));

                if (existingAppOrPatch != null && string.IsNullOrEmpty(row.License))
                    row.License = existingAppOrPatch.License;
            }
        }

        var existingAny = SourceRows.FirstOrDefault();

        if (existingAny != null && !string.IsNullOrEmpty(existingAny.PatchPath))
            row.PatchPath = existingAny.PatchPath;

        _ = row.LoadMetadataAsync();
    }

    private void AddOrReplaceRow(SourceRowViewModel newRow)
    {
        RemoveUnpackedRows();

        if (newRow.Category is ContentCategory.App or ContentCategory.Patch)
        {
            var existing = SourceRows.FirstOrDefault(r => r.Category == newRow.Category);

            if (existing != null)
            {
                AppendLog($"{existing.FileName} -> {newRow.FileName}(으)로 대체됨 (기존 {newRow.Category})");
                SourceRows.Remove(existing);
            }
        }
        else
        {
            var duplicate = SourceRows.FirstOrDefault(r => r.Category == ContentCategory.Addcont && string.Equals(r.TitleId, newRow.TitleId, StringComparison.OrdinalIgnoreCase) && string.Equals(r.ContentIdSuffix, newRow.ContentIdSuffix, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                AppendLog($"{duplicate.FileName} -> {newRow.FileName}(으)로 대체됨 (같은 DLC)");
                SourceRows.Remove(duplicate);
            }
        }

        SourceRows.Insert(ComputeInsertIndex(newRow.Category, newRow.ContentIdSuffix), newRow);
    }

    private void RemoveUnpackedRows()
    {
        var unpackedRows = SourceRows.Where(r => r.IsUnpacked).ToList();

        if (unpackedRows.Count == 0)
            return;

        foreach (var row in unpackedRows)
            SourceRows.Remove(row);

        AppendLog("언팩 폴더 항목을 목록에서 제거했습니다.");
    }

    private int ComputeInsertIndex(ContentCategory category, string? contentIdSuffix)
    {
        if (category == ContentCategory.App)
            return 0;

        if (category == ContentCategory.Patch)
            return SourceRows.Any(r => r.Category == ContentCategory.App) ? 1 : 0;

        int index = SourceRows.Count(r => r.Category is ContentCategory.App or ContentCategory.Patch);

        foreach (var row in SourceRows)
        {
            if (row.Category != ContentCategory.Addcont)
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

            if (row.Category == ContentCategory.Addcont)
            {
                row.IsLicenseEditable = true;
                continue;
            }

            var appRow = SourceRows.FirstOrDefault(r => r.Category == ContentCategory.App && string.Equals(r.TitleId, row.TitleId, StringComparison.OrdinalIgnoreCase));

            row.IsLicenseEditable = appRow == null || appRow.IsPkg;

            if (!row.IsLicenseEditable && !string.IsNullOrEmpty(row.License))
                row.License = string.Empty;
        }
    }

    private void RemoveRow(SourceRowViewModel? row)
    {
        if (row != null)
            SourceRows.Remove(row);
    }

    public async Task StartAsync(BuildMode mode)
    {
        if (!Validate(mode, out string error))
        {
            AppendLog(error, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        _currentMode = mode;
        _totalSw.Restart();

        NotifyButtonStates();

        using (BeginWork())
        {
            try
            {
                CancellationTokenSource = new CancellationTokenSource();

                await ExecuteAsync(mode, CancellationTokenSource.Token);
            }
            finally
            {
                CancellationTokenSource?.Dispose();

                CancellationTokenSource = null;

                _currentMode = null;

                CleanupTask();
                NotifyButtonStates();
            }
        }
    }

    private async Task ExecuteAsync(BuildMode mode, CancellationToken ct)
    {
        string unpackedRoot = UnpackedRoot;
        bool unpackCompleted = false;

        if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedRoot))
        {
            if (!MessageBoxHelper.ShowQuestion("기존 언팩 데이터를 삭제하고 새로 진행할까요?"))
                return;

            Directory.Delete(unpackedRoot, true);
        }

        try
        {
            switch (mode)
            {
                case BuildMode.UnpackOnly:
                    await UnpackAsync(unpackedRoot, ct);
                    unpackCompleted = true;
                    break;
                case BuildMode.RebuildOnly:
                    if (!LoadUnpackedRows(unpackedRoot))
                    {
                        await SoundPlayer.PlayErrorAsync();
                        return;
                    }

                    await BuildAsync(mode, ct);
                    break;
                default:
                    await BuildAsync(mode, ct);
                    break;
            }

            AppendLog($"전체 완료 ({_totalSw.Elapsed:mm\\:ss})", LogLevel.Ok);
            await SoundPlayer.PlayCompleteAsync();
            OutputPath?.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            AppendLog("작업이 취소되었습니다.", LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            MainViewModel.SetTaskbarProgress(0);
            DiscardPartialOutput(mode, unpackCompleted, unpackedRoot);
        }
        catch (Exception ex)
        {
            AppendLog($"오류: {ex.Message}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
            DiscardPartialOutput(mode, unpackCompleted, unpackedRoot);
        }
        finally
        {
            CleanupTask();
        }
    }

    private void DiscardPartialOutput(BuildMode mode, bool unpackCompleted, string unpackedRoot)
    {
        if (mode == BuildMode.UnpackOnly)
        {
            if (!unpackCompleted)
                SafeDeleteDirectory(unpackedRoot);

            return;
        }

        SafeDeleteFile(_pendingZipPath);
    }

    private async Task UnpackAsync(string unpackedRoot, CancellationToken ct)
    {
        var entries = SourceRows.Select(r => r.ToBatchEntry()).ToList();

        Directory.CreateDirectory(unpackedRoot);
        AppendLog("언팩 중...", LogLevel.Highlight);

        int count = await UnpackService.UnpackAsync(entries, unpackedRoot, AppendLog, BuildProgressReporter(), ct);

        AppendLog($"언팩 완료: {count}개 파일 -> {unpackedRoot}", LogLevel.Ok);
    }

    private bool LoadUnpackedRows(string unpackedRoot)
    {
        var titles = UnpackedScanner.Scan(unpackedRoot);

        if (titles.Count == 0)
        {
            AppendLog("언팩된 데이터가 없습니다.", LogLevel.Error);
            return false;
        }

        string patchPath = GetPatchPath();

        SourceRows.Clear();

        foreach (var title in titles)
            SourceRows.Add(SourceRowViewModel.FromUnpacked(title with { PatchPath = patchPath }));

        return true;
    }

    private string GetPatchPath() => SourceRows.Select(r => r.PatchPath).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? string.Empty;

    private async Task BuildAsync(BuildMode mode, CancellationToken ct)
    {
        bool isRebuild = mode == BuildMode.RebuildOnly;
        var target = BuildEmu ? OutputTarget.Emu : OutputTarget.Retail;
        string label = target == OutputTarget.Emu ? "에뮬" : "실기";
        bool merge = MergePatchIntoGame;
        string baseName = SourceRows.First(r => r.Category == ContentCategory.App).TitleId;
        string patchPath = GetPatchPath();
        string suffix = merge ? $"_{label}_merged.zip" : $"_{label}.zip";
        string fileName = VersionInfoExtractor.ApplySuffix($"{baseName}{suffix}", patchPath);
        string zipPath = Utils.GetUniqueFilePath(Path.Combine(OutputPath!, fileName));
        var progress = BuildProgressReporter();

        _pendingZipPath = zipPath;

        if (isRebuild)
        {
            AppendLog($"{label}용 폴더 기반 리팩 중...", LogLevel.Highlight);

            var rebuilt = await RebuildService.RebuildAsync([.. SourceRows.Select(r => r.ToUnpackedTitle())], zipPath, target, merge, AppendLog, progress, ct);

            AppendLog($"{label}용 완료: 총 {rebuilt.TotalFiles}개 파일 (패치 {rebuilt.PatchedSuccessfully}/{rebuilt.PatchCandidates}개 적용) -> {zipPath}", LogLevel.Ok);

            return;
        }

        var entries = SourceRows.Select(r => r.ToBatchEntry()).ToList();

        if (merge)
        {
            AppendLog($"{label}용 게임+패치 병합 중...", LogLevel.Highlight);

            var result = await Task.Run(() => PatchOutputBuilder.BuildMergedFromEntriesAsync(entries, patchPath, zipPath, target, AppendLog, progress, ct), ct);

            AppendLog($"{label}용 완료: 총 {result.TotalFiles}개 파일 (패치 {result.PatchedSuccessfully}/{result.PatchCandidates}개 적용) -> {zipPath}", LogLevel.Ok);
        }
        else
        {
            AppendLog($"{label}용 패치 생성 중...", LogLevel.Highlight);

            var result = await Task.Run(() => PatchOnlyBuilder.BuildFromEntriesAsync(entries, patchPath, zipPath, target, AppendLog, progress, ct), ct);

            AppendLog($"{label}용 완료: 매칭 {result.MatchedCandidates}개 중 {result.PatchedSuccessfully}개 성공 -> {zipPath}", LogLevel.Ok);
        }
    }

    private bool Validate(BuildMode mode, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            error = "작업 폴더를 선택하세요.";
            return false;
        }

        if (mode == BuildMode.RebuildOnly)
        {
            if (!Directory.Exists(UnpackedRoot))
            {
                error = "언팩된 데이터가 없습니다.";
                return false;
            }
        }
        else
        {
            if (SourceRows.Count == 0)
            {
                error = "PS Vita 파일(pkg, zip)/폴더를 하나 이상 추가하세요.";
                return false;
            }

            if (SourceRows.Any(r => r.IsUnpacked))
            {
                error = "언팩 폴더 항목입니다. PS Vita 파일(pkg, zip)/폴더를 다시 추가하세요.";
                return false;
            }
        }

        if (mode == BuildMode.UnpackOnly)
            return true;

        if (!BuildEmu && !BuildRetail)
        {
            error = "출력 포맷을 선택하세요.";
            return false;
        }

        if (mode == BuildMode.FullProcess)
        {
            if (SourceRows.Any(r => string.IsNullOrWhiteSpace(r.PatchPath)))
            {
                error = "패치를 지정하세요.";
                return false;
            }

            bool everyTitleHasApp = SourceRows
                .GroupBy(r => r.TitleId, StringComparer.OrdinalIgnoreCase)
                .All(g => g.Any(r => r.Category == ContentCategory.App));

            if (!everyTitleHasApp)
            {
                error = "App이 없는 타이틀이 있습니다.";
                return false;
            }
        }
        else if (!(BuildEmu && MergePatchIntoGame) && string.IsNullOrWhiteSpace(GetPatchPath()))
        {
            error = "패치를 지정하세요.";
            return false;
        }

        return true;
    }

    private void NotifyButtonStates()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(IsUnpackRunning));
            OnPropertyChanged(nameof(IsRebuildRunning));
            OnPropertyChanged(nameof(IsFullRunning));
            OnPropertyChanged(nameof(UnpackEnabled));
            OnPropertyChanged(nameof(RebuildEnabled));
            OnPropertyChanged(nameof(StartEnabled));
        });
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

    private static void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }
}