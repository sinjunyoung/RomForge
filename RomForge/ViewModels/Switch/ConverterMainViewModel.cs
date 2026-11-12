using Common.Models;
using Common.Localization;
using NSW.Core;
using RomForge.Core;
using RomForge.Core.Models.Switch;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;


namespace RomForge.ViewModels.Switch;

public class ConverterMainViewModel : ToolTabViewModel
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".nsp", ".xci", ".nsz", ".xcz" };

    public ObservableCollection<ConverterFileItem> FileItems { get; } = [];

    public override string? HelpPageKey => "switch-format";

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }

    public event Action<ConverterFileItem>? ScrollToItemRequested;

    public ConverterMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsLocked && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => CancellationTokenSource?.Cancel(), _ => IsLocked);
    }

    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var existing = FileItems.Select(f => f.FilePath)
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newPaths = await Task.Run(() =>
            paths.Where(p => SupportedExtensions.Contains(Path.GetExtension(p)))
                 .Where(p => existing.Add(p))
                 .ToList());

        foreach (var path in newPaths)
        {
            var item = new ConverterFileItem(path)
            {
                FileType = keySet == null ? Loc.S.StatusNoKey : Loc.S.StatusAnalyzing,
            };

            FileItems.Add(item);

            for (int i = 0; i < FileItems.Count; i++)
                FileItems[i].No = i + 1;

            OnPropertyChanged(nameof(HintVisibility));
            CommandManager.InvalidateRequerySuggested();

            if (keySet == null)
                continue;

            var info = await Task.Run(() => MetadataReader.GetGameFileInfo(keySet, path));

            if (info != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    item.TitleName = info.TitleName;
                    item.TitleID = info.TitleId;
                    item.Version = info.DisplayVersion;
                    item.FileType = info.Type;
                    if (info.IconData != null)
                        item.Icon = info.IconData.ToBitmapImage();
                });
            }

            if (string.IsNullOrEmpty(item.TitleName))
                item.TitleName = Path.GetFileNameWithoutExtension(path);
        }
    }

    public void RemoveItems(IEnumerable<ConverterFileItem> items)
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
        CancellationTokenSource?.Dispose();

        CancellationTokenSource = new CancellationTokenSource();

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
                    CancellationTokenSource.Token.ThrowIfCancellationRequested();

                    if (item.State is ItemStatus.Done or ItemStatus.Unsupported)
                        continue;

                    item.State = ItemStatus.Converting;
                    item.Progress = 0;

                    ScrollToItemRequested?.Invoke(item);

                    var progress = new Progress<ProgressInfo>(p => 
                    {
                        if (p.Percent > item.Progress)
                        {
                            item.Progress = p.Percent;
                            MainViewModel.SetTaskbarProgress(item.Progress);
                        }
                    });
                    void Log(string msg, LogLevel level, string? id) => AppendLog(msg, level);

                    try
                    {
                        await ConvertItemAsync(item, progress, Log, CancellationTokenSource.Token);
                        MainViewModel.SetTaskbarProgress(100);

                        item.Progress = 100;
                        item.State = ItemStatus.Done;
                        cnt++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        AppendLog(Loc.S.LogConvertFailed(item.FileName, ex.Message), LogLevel.Error);
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

                foreach (var item in FileItems.Where(i => i.State is ItemStatus.Converting))
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
                CleanupTask();
            }
        }
    }

    private static Task ConvertItemAsync(ConverterFileItem item, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel, string?>? log = null, CancellationToken ct = default)
    {
        string source = item.Extension.ToLower();
        string target = item.SelectedTargetFormat.ToLower();
        var config = AppConfig.Instance.Switch;

        return (source, target) switch
        {
            ("nsp", "xci") => NspXciConvertService.NspToXciAsync(item.FilePath, null, progress, log,  ct),
            ("nsp", "nsz") => NspCompressService.CompressAsync(item.FilePath, null, config.CompressLevel, config.VerifyCompress, config.CompressionMode, progress, log, ct),
            ("nsp", "xcz") => NspXciConvertService.NspToXczAsync(item.FilePath, null, config.CompressLevel, config.VerifyCompress, config.CompressionMode, progress, log, ct),
            ("xci", "nsp") => NspXciConvertService.XciToNspAsync(item.FilePath, null, progress, log, ct),
            ("xci", "xcz") => XciCompressService.CompressAsync(item.FilePath, null, config.CompressLevel, config.VerifyCompress, config.CompressionMode, progress, log, ct),
            ("xci", "nsz") => NspXciConvertService.XciToNszAsync(item.FilePath, null, config.CompressLevel, config.VerifyCompress, config.CompressionMode, progress, log, ct),
            ("nsz", "nsp") => NspCompressService.DecompressAsync(item.FilePath, null, progress, log, ct),
            ("nsz", "xci") => NspXciConvertService.NszToXciAsync(item.FilePath, null, progress, log, ct),
            ("nsz", "xcz") => NspXciConvertService.NszToXczAsync(item.FilePath, null, config.CompressLevel, config.VerifyCompress, config.CompressionMode, progress, log, ct),
            ("xcz", "xci") => XciCompressService.DecompressAsync(item.FilePath, null, progress, log, ct),
            ("xcz", "nsp") => NspXciConvertService.XczToNspAsync(item.FilePath, null, progress, log, ct),
            ("xcz", "nsz") => NspXciConvertService.XczToNszAsync(item.FilePath, null, config.CompressLevel, config.VerifyCompress, config.CompressionMode, progress, log, ct),
            _ => Task.FromException(new NotSupportedException(Loc.S.ErrUnsupportedConversion(source, target)))
        };
    }
}