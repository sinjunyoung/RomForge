using Common;
using N3DS.Core.Crypto;
using N3DS.Core.Models;
using N3DS.Core.Services;
using RomForge.Core.Models._3DS;
using RomForge.Core.Services;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels._3DS;

public class ConverterMainViewModel : ToolTabViewModel
{
    private bool _isConverting;
    private CancellationTokenSource _cts = new();

    public ObservableCollection<FileItem> FileItems { get; } = [];

    public bool IsConverting
    {
        get => _isConverting;
        set { _isConverting = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }

    public event Action<FileItem>? ScrollToItemRequested;
    public event EventHandler RunNavigateCerts;

    public ConverterMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);
    }

    public async Task AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Utils.ExpandPaths(paths))
        {
            if (!InstallMainViewModel.SupportedExtensions.Contains(Path.GetExtension(path)))
                continue;

            if (!existing.Add(path))
                continue;

            try
            {
                var result = await Task.Run(() => Core.Services._3DS.Util.ParseFile(path));
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

                FileItems.Add(vm);
            }
            catch (Exception ex)
            {
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
            }
        }

        for (int i = 0; i < FileItems.Count; i++)
            FileItems[i].No = i + 1;

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    public void RemoveItems(IEnumerable<FileItem> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        for (int i = 0; i < FileItems.Count; i++)
            FileItems[i].No = i + 1;

        OnPropertyChanged(nameof(HintVisibility));
    }

    public void ClearItems()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
    }

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

                    var progressHandler = new Progress<ProgressInfo>(p => { item.Progress = p.Percent; MainViewModel.SetTaskbarProgress(p.Percent); });
                    string inputExt = item.Extension;

                    try
                    {
                        switch (item.SelectedTargetFormat)
                        {
                            case "CIA":
                                {
                                    KeyStore key = new();
                                    CciToCiaConverter c = new(key);

                                    await c.ConvertAsync(item.FilePath, progressHandler, AppendLog, null, _cts.Token);
                                }
                                break;
                            case "ZCCI":
                                {
                                    if (inputExt == "cia")
                                        await Z3dsCompressor.CompressFromCiaAsync(item.FilePath, 18, progressHandler, AppendLog, null, _cts.Token);
                                    else
                                        await Z3dsCompressor.CompressAsync(item.FilePath, 18, progressHandler, AppendLog, null, _cts.Token);
                                }
                                break;
                            case "CCI":
                                {
                                    if (inputExt == "zcci")
                                        await Z3dsDecompressor.DecompressAsync(item.FilePath, progressHandler, AppendLog, null, _cts.Token);
                                    else if (inputExt == "cia")
                                    {
                                        KeyStore key = new();
                                        var ciaToCci = new CiaToCciConverter(key);

                                        await ciaToCci.ConvertAsync(item.FilePath, progressHandler, AppendLog, null, _cts.Token);
                                    }
                                }
                                break;
                            default:
                                throw new NotSupportedException("잘못된 출력 포맷 설정입니다.");
                        }

                        item.Progress = 100;
                        MainViewModel.SetTaskbarProgress(100);

                        item.Status = "완료";                        
                        cnt++;
                    }
                    catch (CertsBinNotFoundException e)
                    {
                        AppendLog(e.Message, LogLevel.Error);
                        await SoundPlayer.PlayErrorAsync();
                        MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                        RunNavigateCerts?.Invoke(this, EventArgs.Empty);                        
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"[{item.FileName}.{item.Extension}] 변환 실패: {ex.Message}", LogLevel.Error);
                        await SoundPlayer.PlayErrorAsync();
                        MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);

                        item.Status = "실패";
                        item.Progress = 0;                        
                    }
                }

                if (cnt > 0)
                {
                    AppendLog($"총 {cnt}개의 작업을 성공적으로 완료했습니다.", LogLevel.Ok);
                    await SoundPlayer.PlayCompleteAsync();
                    MainViewModel.SetTaskbarProgress(100);
                }
                else
                    AppendLog("성공한 작업이 없습니다.", LogLevel.Error);

            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
                MainViewModel.SetTaskbarProgress(0);

                foreach (var item in FileItems.Where(i => i.Status == "대기중" || i.Status == "변환중"))
                    item.Status = "취소";
            }
            catch (Exception ex)
            {
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);

                foreach (var item in FileItems.Where(i => i.Status == "변환중"))
                    item.Status = "실패";
            }
            finally
            {
                IsConverting = false;

                CleanupTask();
            }
        }
    }
}