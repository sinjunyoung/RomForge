using _3DS.Core.Crypto;
using _3DS.Core.Services;
using Common;
using Common.WPF.ViewModels;
using RomForge.Helpers;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels._3DS;

public class ConverterMainViewModel : ToolTabViewModel
{
    #region Fields

    private bool _isConverting;
    private CancellationTokenSource _cts = new();

    #endregion

    #region Collections

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ObservableCollection<FileItem> FileItems { get; } = [];

    #endregion

    #region Properties

    public bool IsConverting
    {
        get => _isConverting;
        set { _isConverting = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region Commands

    public ICommand RunCommand { get; }

    #endregion

    public event Action<FileItem>? ScrollToItemRequested;

    #region Constructor

    public ConverterMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);
    }

    #endregion

    #region Public Methods

    public async Task AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ExpandPaths(paths))
        {
            if (!InstallMainViewModel.SupportedExtensions.Contains(Path.GetExtension(path)))
                continue;

            if (!existing.Add(path))
                continue;

            try
            {
                var result = await Util.ParseFile(path);
                var vm = new FileItem(path)
                {
                    TitleId = result.Title!.TitleId,
                    ProductCode = result.ProductCode,
                    ShortDescription = result.ShortDescription,
                    Publisher = result.Publisher,
                    Crypto = result.Crypto
                };

                if (result?.IconPixels is not null)
                {
                    var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, result?.IconPixels, 48 * 4);
                    bitmap.Freeze();
                    vm.Icon = bitmap;
                }

                vm.No = FileItems.Count + 1;

                FileItems.Add(vm);
            }
            catch (Exception ex)
            {
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
            }
        }

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    public void RemoveItems(IEnumerable<FileItem> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        OnPropertyChanged(nameof(HintVisibility));
    }

    public void ClearItems()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
    }

    #endregion

    #region Private Methods

    private async Task RunAsync()
    {
        IsConverting = true;
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        ClearLog();

        using (BeginWork())
        {
            try
            {
                int totalCount = FileItems.Count;
                AppendLog($"총 {totalCount}개의 3DS 변환 작업을 시작합니다.", LogLevel.Highlight);

                int cnt = 0;
                foreach (var item in FileItems)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    if (item.Status == "완료" || item.Status == "미지원")
                        continue;

                    item.Status = "대기중";
                    item.Progress = 0;
                    item.Status = "변환중";
                    ScrollToItemRequested?.Invoke(item);

                    var progressHandler = new Progress<ProgressInfo>(p => { item.Progress = p.Percent; });
                    string inputExt = item.Extension;

                    try
                    {
                        void logWrapper(string msg, LogLevel level, string id) => AppendLog(msg, level);

                        switch (item.SelectedTargetFormat)
                        {
                            case "CIA":
                                {
                                    KeyStore key = new();
                                    CciToCiaConverter c = new(key);
                                    await c.ConvertAsync(item.FilePath, progressHandler, logWrapper, _cts.Token);
                                }
                                break;

                            case "ZCCI":
                                {
                                    if (inputExt == "cia")
                                        await Z3dsArchiveService.CompressFromCiaAsync(item.FilePath, 18, progressHandler, logWrapper, _cts.Token);
                                    else
                                        await Z3dsArchiveService.CompressAsync(item.FilePath, 18, progressHandler, logWrapper, _cts.Token);
                                }
                                break;

                            case "CCI":
                                {
                                    if (inputExt == "zcci")
                                        await Z3dsArchiveService.DecompressAsync(item.FilePath, progressHandler, logWrapper, _cts.Token);
                                    else if (inputExt == "cia")
                                    {
                                        KeyStore key = new();
                                        var ciaToCci = new CiaToCciConverter(key);
                                        await ciaToCci.ConvertAsync(item.FilePath, progressHandler, logWrapper, _cts.Token);
                                    }
                                }
                                break;

                            default:
                                throw new NotSupportedException("잘못된 출력 포맷 설정입니다.");
                        }

                        item.Progress = 100;
                        item.Status = "완료";
                        cnt++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"[{item.FileName}.{item.Extension}] 변환 실패: {ex.Message}", LogLevel.Error);
                        item.Status = "실패";
                        item.Progress = 0;
                    }
                }

                if (cnt > 0)
                {
                    AppendLog($"총 {cnt}개의 작업을 성공적으로 완료했습니다.", LogLevel.Ok);
                }
                else
                {
                    AppendLog("성공한 작업이 없습니다.", LogLevel.Error);
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status == "대기중" || i.Status == "변환중"))
                    item.Status = "취소";
            }
            catch (Exception ex)
            {
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status == "변환중"))
                    item.Status = "실패";
            }
            finally
            {
                IsConverting = false;
            }
        }
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*.*", options))
                    yield return f;
            else if (File.Exists(path))
                yield return path;
        }
    }

    private void AppendLog(string msg, LogLevel level = LogLevel.Info)
    {
        if (Application.Current?.Dispatcher == null) 
            return;

        Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
    }

    private void ClearLog()
    {
        if (Application.Current?.Dispatcher == null) 
            return;

        Application.Current.Dispatcher.Invoke(() => LogEntries.Clear());
    }

    #endregion
}