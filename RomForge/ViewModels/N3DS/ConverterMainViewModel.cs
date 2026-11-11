using Common.Localization;
using Common.Models;
using Common.Services;
using N3DS.Core.Crypto;
using N3DS.Core.Models;
using N3DS.Core.Services;
using RomForge.Core;
using RomForge.Core.Models.N3DS;
using RomForge.Core.Services;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels.N3DS;

public class ConverterMainViewModel : ToolTabViewModel
{
    private CancellationTokenSource _cts = new();

    public ObservableCollection<FileItem> FileItems { get; } = [];

    public bool IsConverting { get; set { SetPropertyAndRequery(ref field, value); } }

    public override string? HelpPageKey => "3ds-format";

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }

    public event Action<FileItem>? ScrollToItemRequested;
    public event EventHandler? RunNavigateCerts;

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
                var result = await Task.Run(() => Core.Services.N3DS.Util.ParseFile(path));
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
                AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
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

                AppendLog(Loc.S.LogJobsStartingConvert(totalCount), LogLevel.Highlight);

                int cnt = 0;

                foreach (var item in FileItems)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    if (item.State is ItemStatus.Done or ItemStatus.Unsupported)
                        continue;

                    item.State = ItemStatus.Waiting;
                    item.Progress = 0;
                    item.State = ItemStatus.Converting;

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

                                    await c.ConvertAsync(item.FilePath, null, progressHandler, AppendLog, _cts.Token);
                                }
                                break;
                            case "ZCCI":
                                {
                                    if (inputExt == "cia")
                                        await Z3dsCompressor.CompressFromCiaAsync(item.FilePath, null, AppConfig.Instance.Azahar.CompressLevel, progressHandler, AppendLog, _cts.Token);
                                    else
                                        await Z3dsCompressor.CompressAsync(item.FilePath, null, AppConfig.Instance.Azahar.CompressLevel, progressHandler, AppendLog, _cts.Token);
                                }
                                break;
                            case "CCI":
                                {
                                    if (inputExt == "zcci")
                                        await Z3dsDecompressor.DecompressAsync(item.FilePath, null, progressHandler, AppendLog, _cts.Token);
                                    else if (inputExt == "cia")
                                    {
                                        KeyStore key = new();
                                        var ciaToCci = new CiaToCciConverter(key);

                                        await ciaToCci.ConvertAsync(item.FilePath, null, progressHandler, AppendLog, _cts.Token);
                                    }
                                }
                                break;
                            default:
                                throw new NotSupportedException(Loc.S.ErrInvalidOutputFormat);
                        }

                        item.Progress = 100;
                        MainViewModel.SetTaskbarProgress(100);

                        item.State = ItemStatus.Done;                        
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
                        AppendLog(Loc.S.LogConvertFailed($"{item.FileName}.{item.Extension}", ex.Message), LogLevel.Error);
                        await SoundPlayer.PlayErrorAsync();
                        MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);

                        item.State = ItemStatus.Failed;
                        item.Progress = 0;                        
                    }
                }

                if (cnt > 0)
                {
                    AppendLog(Loc.S.LogJobsCompleted(cnt), LogLevel.Ok);
                    await SoundPlayer.PlayCompleteAsync();
                    MainViewModel.SetTaskbarProgress(100);
                }
                else
                    AppendLog(Loc.S.LogNoSuccessfulJobs, LogLevel.Error);

            }
            catch (OperationCanceledException)
            {
                AppendLog(Loc.S.LogJobCancelled, LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
                MainViewModel.SetTaskbarProgress(0);

                foreach (var item in FileItems.Where(i => i.State is ItemStatus.Waiting or ItemStatus.Converting))
                    item.State = ItemStatus.Cancelled;
            }
            catch (Exception ex)
            {
                AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);

                foreach (var item in FileItems.Where(i => i.State is ItemStatus.Converting))
                    item.State = ItemStatus.Failed;
            }
            finally
            {
                IsConverting = false;

                CleanupTask();
            }
        }
    }
}