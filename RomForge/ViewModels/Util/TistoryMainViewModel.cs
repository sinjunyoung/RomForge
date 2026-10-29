using Common;
using RomForge.Core;
using RomForge.Core.Models.Util;
using RomForge.Core.Services;
using RomForge.Core.Services.Util;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Util;

public class TistoryMainViewModel : BatchToolTabViewModel
{
    private static readonly char[] separator = ['\r', '\n'];
    private CancellationTokenSource _cts = new();

    public ObservableCollection<TistoryDownloadItem> FileItems { get; } = [];

    public string PageUrls { get; set { SetPropertyAndRequery(ref field, value); } } = string.Empty;

    public string SaveDirectory
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                AppConfig.Instance.Tistory.SaveDirectory = value;
        }
    } = string.IsNullOrWhiteSpace(AppConfig.Instance.Tistory.SaveDirectory) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TistoryDownloads") : AppConfig.Instance.Tistory.SaveDirectory;

    public bool IsBusy
    {
        get;
        set { SetProperty(ref field, value); OnPropertyChanged(nameof(IsLocked)); CommandManager.InvalidateRequerySuggested(); }
    }

    public bool AutoExtractAfterDownload
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                AppConfig.Instance.Tistory.AutoExtractAfterDownload = value;
        }
    } = AppConfig.Instance.Tistory.AutoExtractAfterDownload;

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand AnalyzeCommand { get; }

    public ICommand RunCommand { get; }

    public event Action<TistoryDownloadItem>? ScrollToItemRequested;

    public TistoryMainViewModel()
    {
        AnalyzeCommand = new RelayCommand(async _ => await AnalyzeAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(PageUrls));
        RunCommand = new RelayCommand(async _ => await DownloadSelectedAsync(), _ => !IsBusy && FileItems.Any(i => i.IsSelected));
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsBusy);
    }

    public void RemoveItems(IEnumerable<TistoryDownloadItem> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        Renumber();
        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    public void ClearItems()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    public void ChangeSaveDirectory(string dir) => SaveDirectory = dir;

    private async Task AnalyzeAsync()
    {
        IsBusy = true;

        _cts.Dispose();

        _cts = new CancellationTokenSource();

        bool canceled = false;
        bool failed = false;

        using (BeginWork())
        {
            try
            {
                BeginBatch(1);
                StartBatchItem();

                var urlsList = PageUrls
                    .Split(separator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(u => u.Trim())
                    .Where(u => !string.IsNullOrEmpty(u))
                    .ToList();

                AppendLog($"[분석 시작] 총 {urlsList.Count}개 URL", LogLevel.Highlight);

                var urls = await TistoryAttachmentService.ExtractAttachmentUrlsAsync(urlsList, msg => AppendLog(msg), _cts.Token);
                var excludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif" };
                var existing = FileItems.Select(f => f.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var addedCount = 0;

                foreach (var url in urls)
                {
                    try
                    {
                        var uri = new Uri(url);
                        var ext = Path.GetExtension(uri.LocalPath);

                        if (!string.IsNullOrEmpty(ext) && excludedExtensions.Contains(ext))
                            continue;
                    }
                    catch { }

                    if (!existing.Add(url))
                        continue;

                    var fileName = TistoryAttachmentService.GetCachedFileName(url);
                    var fileSize = TistoryAttachmentService.GetCachedFileSize(url);
                    var item = new TistoryDownloadItem(url)
                    {
                        FileName = fileName,
                        FileSizeBytes = fileSize
                    };

                    item.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(TistoryDownloadItem.IsSelected))
                            CommandManager.InvalidateRequerySuggested();
                    };

                    FileItems.Add(item);

                    addedCount++;
                }

                Renumber();

                if (FileItems.Count > 0)
                    ScrollToItemRequested?.Invoke(FileItems[^1]);

                if (addedCount > 0)
                {
                    AppendLog($"[분석 완료] 첨부파일 {addedCount}개를 추가했습니다.", LogLevel.Highlight);
                    await SoundPlayer.PlayCompleteAsync();
                    CompleteBatchItem();
                }
                else
                    AppendLog("[분석 완료] 새로 추가된 첨부파일이 없습니다.", LogLevel.Error);
            }
            catch (OperationCanceledException)
            {
                canceled = true;

                AppendLog("분석이 취소되었습니다.", LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
            }
            catch (Exception ex)
            {
                AppendLog($"[분석 실패] {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                failed = true;
            }
            finally
            {
                IsBusy = false;

                EndBatch(canceled, failed);

                OnPropertyChanged(nameof(HintVisibility));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private async Task DownloadSelectedAsync()
    {
        IsBusy = true;

        _cts.Dispose();

        _cts = new CancellationTokenSource();

        bool canceled = false;
        bool failed = false;

        using (BeginWork())
        {
            try
            {
                var targetItems = FileItems.Where(i => i.IsSelected).ToList();
                int totalCount = targetItems.Count;
                int successCount = 0;
                object lockObj = new();

                AppendLog($"선택된 총 {totalCount}개의 첨부파일 다운로드를 시작합니다. (최대 5개 병렬)", LogLevel.Highlight);

                BeginBatch(targetItems.Count(i => i.Status != "완료"));

                var options = new ParallelOptions
                {
                    CancellationToken = _cts.Token,
                    MaxDegreeOfParallelism = 5
                };

                await Parallel.ForEachAsync(targetItems, options, async (item, token) =>
                {
                    token.ThrowIfCancellationRequested();

                    if (item.Status == "완료")
                        return;

                    item.Status = "다운로드중";
                    item.Progress = 0;
                    BatchItemStarted(item);

                    ScrollToItemRequested?.Invoke(item);

                    var progress = new Progress<int>(p =>
                    {
                        item.Progress = p;
                        BatchItemProgress(item, p);
                    });

                    try
                    {
                        var (savedPath, sizeBytes) = await TistoryAttachmentService.DownloadAsync(item.Url, SaveDirectory, item.FileName, progress, token);

                        item.SavedPath = savedPath;
                        item.FileName = Path.GetFileName(savedPath);
                        item.FileSizeBytes = sizeBytes;
                        item.Progress = 100;
                        item.Status = "완료";
                        BatchItemCompleted(item);

                        lock (lockObj)
                            successCount++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        item.Progress = 0;
                        item.Status = "실패";
                        failed = true;
                        BatchItemCompleted(item, false);
                    }
                });

                AppendLog($"다운로드 작업 완료 (성공: {successCount} / 전체: {totalCount})", LogLevel.Ok);

                if (AutoExtractAfterDownload)
                {
                    AppendLog("압축해제를 시작합니다...", LogLevel.Highlight);

                    var (success, total) = await ExtractCompletedArchivesAsync(targetItems, _cts.Token);
                    int failCount = total - success;

                    AppendLog($"압축해제 완료 (성공: {success} / 실패: {failCount} / 전체 그룹: {total})", failCount == 0 ? LogLevel.Ok : LogLevel.Error);
                }

                AppendLog("모든 작업이 종료되었습니다.", LogLevel.Highlight);

                if (successCount > 0)
                {
                    await SoundPlayer.PlayCompleteAsync();
                    SaveDirectory.OpenFolder();
                }
            }
            catch (OperationCanceledException)
            {
                canceled = true;

                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();

                foreach (var item in FileItems.Where(i => i.Status == "대기중" || i.Status == "다운로드중"))
                {
                    item.Status = "취소";
                    item.Progress = 0;
                }
            }
            finally
            {
                IsBusy = false;

                EndBatch(canceled, failed);
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private async Task<(int success, int total)> ExtractCompletedArchivesAsync(List<TistoryDownloadItem> targetItems, CancellationToken token)
    {
        int successCount = 0;
        var downloaded = targetItems.Where(i => i.Status == "완료" && !string.IsNullOrWhiteSpace(i.SavedPath)).ToList();
        var groups = downloaded.GroupBy(i => TistoryArchiveExtractor.GetGroupKey(i.FileName)).ToList();
        int totalGroups = groups.Count;

        BeginBatch(totalGroups);

        foreach (var group in groups)
        {
            token.ThrowIfCancellationRequested();

            var items = group.ToList();
            var representative = items[0];

            if (!TistoryArchiveExtractor.IsArchiveFile(representative.SavedPath))
            {
                BatchItemCompleted(group);
                continue;
            }

            bool isMultiVolume = TistoryArchiveExtractor.IsVolumePart(representative.FileName);

            if (isMultiVolume && !TistoryArchiveExtractor.HasContiguousParts(items.Select(i => i.FileName)))
            {
                AppendLog($"[압축해제 건너뜀] {representative.FileName} 계열의 볼륨 파일이 일부만 선택되어 있습니다.", LogLevel.Error);

                foreach (var item in items)
                    item.Status = "압축해제실패";

                BatchItemCompleted(group, false);

                continue;
            }

            var firstItem = isMultiVolume ? items.First(i => TistoryArchiveExtractor.IsFirstVolumePart(i.FileName)) : representative;

            try
            {
                var allPaths = items.Select(i => i.SavedPath!).ToList();

                foreach (var item in items)
                {
                    item.Progress = 0;
                    item.Status = "압축해제중";
                }

                BatchItemStarted(group);

                ScrollToItemRequested?.Invoke(representative);

                var extractProgress = new Progress<int>(p =>
                {
                    foreach (var item in items)
                        item.Progress = p;

                    BatchItemProgress(group, p);
                });

                var extractDir = await Task.Run(() => TistoryArchiveExtractor.ExtractAndDeleteSource(firstItem.SavedPath!, allPaths, extractProgress), token);
                var displayName = Path.GetFileName(extractDir);

                foreach (var item in items)
                {
                    item.SavedPath = extractDir;
                    item.FileName = displayName;
                    item.Progress = 100;
                    item.Status = "압축해제완료";
                }

                successCount++;
                BatchItemCompleted(group);
                AppendLog($"[압축해제] {displayName} 완료", LogLevel.Ok);
            }
            catch (Exception ex)
            {
                foreach (var item in items)
                {
                    item.Progress = 0;
                    item.Status = "압축해제실패";
                }

                AppendLog($"[압축해제 실패] {representative.FileName}: {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                BatchItemCompleted(group, false);
            }
        }

        return (successCount, totalGroups);
    }

    private void Renumber()
    {
        for (int i = 0; i < FileItems.Count; i++)
            FileItems[i].No = i + 1;
    }
}