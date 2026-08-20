using Common;
using Common.WPF.ViewModels;
using RomForge.Core.Models;
using RomForge.Core.Models.Util;
using RomForge.Core.Services.Util;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Util;

public class TistoryMainViewModel : ToolTabViewModel
{
    private string _pageUrl = string.Empty;
    private string _saveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TistoryDownloads");
    private bool _isBusy;
    private bool _autoExtractAfterDownload;
    private CancellationTokenSource _cts = new();

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ObservableCollection<TistoryDownloadItem> FileItems { get; } = [];

    public string PageUrl
    {
        get => _pageUrl;
        set { SetProperty(ref _pageUrl, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public string SaveDirectory
    {
        get => _saveDirectory;
        set => SetProperty(ref _saveDirectory, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLocked)); CommandManager.InvalidateRequerySuggested(); }
    }

    public bool AutoExtractAfterDownload
    {
        get => _autoExtractAfterDownload;
        set => SetProperty(ref _autoExtractAfterDownload, value);
    }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand AnalyzeCommand { get; }

    public ICommand RunCommand { get; }

    public event Action<TistoryDownloadItem>? ScrollToItemRequested;

    public TistoryMainViewModel()
    {
        AnalyzeCommand = new RelayCommand(async _ => await AnalyzeAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(PageUrl));
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

        using (BeginWork())
        {
            try
            {
                AppendLog($"[분석 시작] {PageUrl}", LogLevel.Highlight);

                var urls = await TistoryAttachmentService.ExtractAttachmentUrlsAsync(PageUrl, msg => AppendLog(msg), _cts.Token);
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
                    catch
                    {
                    }

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

                AppendLog(addedCount > 0 ? $"[분석 완료] 첨부파일 {addedCount}개를 추가했습니다." : "[분석 완료] 새로 추가된 첨부파일이 없습니다.", addedCount > 0 ? LogLevel.Highlight : LogLevel.Error);
            }
            catch (OperationCanceledException)
            {
                AppendLog("분석이 취소되었습니다.", LogLevel.Error);
            }
            catch (Exception ex)
            {
                AppendLog($"[분석 실패] {ex.Message}", LogLevel.Error);
            }
            finally
            {
                IsBusy = false;

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

        using (BeginWork())
        {
            try
            {
                var targetItems = FileItems.Where(i => i.IsSelected).ToList();
                int totalCount = targetItems.Count;
                int successCount = 0;
                object lockObj = new();

                AppendLog($"선택된 총 {totalCount}개의 첨부파일 다운로드를 시작합니다. (최대 5개 병렬)", LogLevel.Highlight);

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

                    ScrollToItemRequested?.Invoke(item);

                    var progress = new Progress<int>(p => item.Progress = p);

                    try
                    {
                        var (savedPath, sizeBytes) = await TistoryAttachmentService.DownloadAsync(item.Url, SaveDirectory, item.FileName, progress, token);

                        item.SavedPath = savedPath;
                        item.FileName = Path.GetFileName(savedPath);
                        item.FileSizeBytes = sizeBytes;
                        item.Progress = 100;
                        item.Status = "완료";

                        lock (lockObj)
                            successCount++;

                        AppendLog($"[성공] {item.FileName} 저장 완료");

                        if (AutoExtractAfterDownload && TistoryArchiveExtractor.IsArchiveFile(savedPath))
                        {
                            try
                            {
                                item.Status = "압축해제중";

                                var extractDir = await Task.Run(() => TistoryArchiveExtractor.ExtractAndDeleteSource(savedPath), token);

                                item.SavedPath = extractDir;
                                item.FileName = Path.GetFileName(extractDir);
                                item.Status = "완료";

                                AppendLog($"[압축해제] {item.FileName} 완료 (원본 삭제됨)");
                            }
                            catch (Exception ex)
                            {
                                item.Status = "완료";

                                AppendLog($"[압축해제 실패] {Path.GetFileName(savedPath)}: {ex.Message}", LogLevel.Error);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        item.Progress = 0;
                        item.Status = "실패";

                        AppendLog($"[실패] {item.FileName}: {ex.Message}", LogLevel.Error);
                    }
                });

                AppendLog($"작업 완료 (성공: {successCount} / 전체: {totalCount})", LogLevel.Highlight);
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);

                foreach (var item in FileItems.Where(i => i.Status == "대기중" || i.Status == "다운로드중"))
                {
                    item.Status = "취소";
                    item.Progress = 0;
                }
            }
            finally
            {
                IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
    private void Renumber()
    {
        for (int i = 0; i < FileItems.Count; i++)
            FileItems[i].No = i + 1;
    }

    private void AppendLog(string msg, LogLevel level = LogLevel.Info)
    {
        if (Application.Current?.Dispatcher == null)
            return;

        Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
    }
}