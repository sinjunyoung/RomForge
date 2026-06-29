using Common;
using Common.WPF.ViewModels;
using PSP.Core.Services;
using RomForge.Core;
using RomForge.Helpers;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.PS;

public class PSPConverterViewModel : ToolTabViewModel
{
    #region Fields

    private readonly AppConfig _config;
    private bool _isConverting;
    private CancellationTokenSource _cts = new();
    private readonly CsoService _csoService = new();

    private static readonly HashSet<string> SupportedExtensions =
        [".iso", ".cso", ".chd"];

    #endregion

    #region Collections

    public ObservableCollection<LogEntry> LogEntries { get; } = [];
    public ObservableCollection<PspFileItem> FileItems { get; } = [];

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

    public event Action<PspFileItem>? ScrollToItemRequested;

    #region Constructor

    public PSPConverterViewModel(AppConfig config)
    {
        _config = config;
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);
    }

    #endregion

    #region Public Methods

    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ExpandPaths(paths))
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                continue;

            if (!existing.Add(path))
                continue;

            var item = new PspFileItem(path);

            FileItems.Add(item);

            for (int i = 0; i < FileItems.Count; i++)
                FileItems[i].No = i + 1;
        }

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    public void RemoveItems(IEnumerable<PspFileItem> items)
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
                AppendLog($"총 {totalCount}개의 PSP 변환 작업을 시작합니다.", LogLevel.Highlight);

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

                    var progressHandler = new Progress<double>(p => item.Progress = (int)(p * 100));
                    string inputExt = item.Extension.ToLowerInvariant();
                    string outputExt = item.SelectedTargetFormat.ToLowerInvariant();
                    string outPath = Path.ChangeExtension(item.FilePath, outputExt);
                    outPath = Utils.GetUniqueFilePath(outPath);

                    try
                    {
                        switch ((inputExt, outputExt))
                        {
                            case ("iso", "cso"):
                                await using (var input = File.OpenRead(item.FilePath))
                                await using (var output = File.Create(outPath))
                                    await CsoService.CompressAsync(input, output, progress: progressHandler, ct: _cts.Token);
                                break;

                            case ("iso", "chd"):
                                await _csoService.CompressToChdAsync(item.FilePath, outPath, _config.Chdman.Compression, _cts.Token);
                                break;

                            case ("cso", "iso"):
                                await using (var input = File.OpenRead(item.FilePath))
                                await using (var output = File.Create(outPath))
                                    await CsoService.DecompressAsync(input, output, progressHandler, _cts.Token);
                                break;

                            case ("cso", "chd"):
                                await _csoService.CompressCsoToChdAsync(item.FilePath, outPath, progressHandler, _config.Chdman.Compression, _cts.Token);
                                break;

                            case ("chd", "iso"):
                                await _csoService.ExtractChdToIsoAsync(item.FilePath, outPath, progressHandler, _cts.Token);
                                break;

                            case ("chd", "cso"):
                                await using (var output = File.Create(outPath))
                                    await CsoService.CompressFromChdAsync(item.FilePath, output, version: 1, progressHandler, _cts.Token);
                                break;

                            default:
                                throw new NotSupportedException($"{inputExt} → {outputExt} 변환은 지원하지 않습니다.");
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
                        AppendLog($"[{item.FileName}] 변환 실패: {ex.Message}", LogLevel.Error);
                        item.Status = "실패";
                        item.Progress = 0;
                    }
                }

                AppendLog(cnt > 0
                    ? $"총 {cnt}개의 작업을 성공적으로 완료했습니다."
                    : "성공한 작업이 없습니다.", cnt > 0 ? LogLevel.Ok : LogLevel.Error);
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status is "대기중" or "변환중"))
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
        if (Application.Current?.Dispatcher == null) return;
        Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
    }

    private void ClearLog()
    {
        if (Application.Current?.Dispatcher == null) return;
        Application.Current.Dispatcher.Invoke(() => LogEntries.Clear());
    }

    #endregion
}