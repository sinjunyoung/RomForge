using _3DS.Core.Services;
using CHD.Core.Services;
using Common;
using Common.WPF.ViewModels;
using DolphinTool.Core.Services;
using RomForge.Helpers;
using RomForge.Models;
using RomZip.Core.Enums;
using RomZip.Core.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels;

public class CompressMainViewModel : ToolTabViewModel
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".iso", ".cue", ".gdi", ".chd",
        ".nsp", ".nsz", ".xci", ".xcz",
        ".gcm", ".wbfs", ".gcz", ".wia", ".rvz",
        ".3ds", ".cci", ".cia", ".zcci"
    };

    private CancellationTokenSource _cts = new();
    private readonly Core.AppConfig _config;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];
    public ObservableCollection<CompressFileItem> FileItems { get; } = [];

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }

    public event Action<CompressFileItem>? ScrollToItemRequested;

    public CompressMainViewModel(Core.AppConfig config)
    {
        _config = config;
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsLocked && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsLocked);
    }

    public static string GetFileDialogFilter()
    {
        string wildcards = string.Join(";", SupportedExtensions.Select(ext => $"*{ext}"));

        return $"지원 파일|{wildcards}|모든 파일|*.*";
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ExpandPaths(paths))
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                continue;

            if (!existing.Add(path))
                continue;

            var item = new CompressFileItem(path) { No = FileItems.Count + 1 };
            FileItems.Add(item);
        }

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    public void RemoveItems(IEnumerable<CompressFileItem> items)
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

    private async Task RunAsync()
    {
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        ClearLog();

        using (BeginWork())
        {
            int totalCount = FileItems.Count;
            AppendLog($"총 {totalCount}개의 작업을 시작합니다.", LogLevel.Highlight);

            int cnt = 0;

            foreach (var item in FileItems)
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    CancelRemainingItems();
                    break;
                }

                if (item.Status == "완료" || item.Status == "미지원")
                    continue;

                try
                {
                    item.Status = "대기중";
                    item.Progress = 0;

                    var detected = FormatDetector.Detect(item.FilePath);

                    item.Status = "변환중";
                    ScrollToItemRequested?.Invoke(item);

                    var progressHandler = new Progress<ProgressInfo>(p =>
                    {
                        item.Progress = p.Percent;
                    });

                    int switchCompressLevel = _config.Switch.CompressLevel;
                    if (switchCompressLevel < 3)
                        switchCompressLevel = 3;

                    switch (detected.Format)
                    {
                        case RomFormat.Nsp:
                            await NspCompressService.CompressAsync(item.FilePath, _config.Switch.CompressLevel, _config.Switch.VerifyCompress, _config.Switch.UseBlockMode, progressHandler, AppendLog, _cts.Token);
                            break;
                        case RomFormat.Xci:
                            await XciCompressService.CompressAsync(item.FilePath, _config.Switch.CompressLevel, _config.Switch.VerifyCompress, _config.Switch.UseBlockMode, progressHandler, AppendLog, _cts.Token);
                            break;
                        case RomFormat.Nsz:
                            await NspCompressService.DecompressAsync(item.FilePath, progressHandler, AppendLog, _cts.Token);
                            break;
                        case RomFormat.Xcz:
                            await XciCompressService.DecompressAsync(item.FilePath, progressHandler, AppendLog, _cts.Token);
                            break;
                        case RomFormat.Cci:
                            await Z3dsArchiveService.CompressAsync(item.FilePath, _config.Azahar.CompressLevel, progressHandler, AppendLog, _cts.Token);
                            break;
                        case RomFormat.Cia:
                            await Z3dsArchiveService.CompressFromCiaAsync(item.FilePath, _config.Azahar.CompressLevel, progressHandler, AppendLog, _cts.Token);
                            break;
                        case RomFormat.ZCci:
                            await Z3dsArchiveService.DecompressAsync(item.FilePath, progressHandler, AppendLog, _cts.Token);
                            break;
                        case RomFormat.Iso:
                        case RomFormat.Cue:
                        case RomFormat.Gdi:
                        case RomFormat.Chd:
                            {
                                FileConverter chdConverter = new();

                                chdConverter.LogMessage += (_, e) => AppendLog(e.Message, e.Level);
                                chdConverter.ProgressChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => item.Progress = e.Progress);

                                var chdResult = await chdConverter.ConvertFileAsync(item.FilePath, _cts.Token);

                                if (!chdResult.Success)
                                    throw new InvalidOperationException(chdResult.Message);
                            }
                            break;
                        case RomFormat.Rvz:
                        case RomFormat.Wii:
                        case RomFormat.Gcm:
                        case RomFormat.Gcz:
                        case RomFormat.Wbfs:
                        case RomFormat.Wia:
                            {
                                DolphinService dolphin = new();

                                dolphin.LogMessage += (_, e) => AppendLog(e.Message, e.Level);
                                dolphin.ProgressChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => item.Progress = e.Progress);

                                await dolphin.ConvertFileAsync(item.FilePath, detected.Format.ToString(), detected.OutputExtension, _config.Dolphin.CompressLevel, _cts.Token);
                            }
                            break;
                        case RomFormat.Unknown:
                        default:
                            item.Status = "미지원";
                            AppendLog($"[{item.FileName}] 지원하지 않는 포맷", LogLevel.Error);

                            continue;
                    }

                    item.Progress = 100;
                    item.Status = "완료";
                    cnt++;
                }
                catch (OperationCanceledException)
                {
                    AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                    CancelRemainingItems();

                    break;
                }
                catch (Exception ex)
                {
                    AppendLog($"오류 ([{item.FileName}]): {ex.Message}", LogLevel.Error);
                    item.Status = "실패";
                }
            }

            if (cnt > 0)
            {
                AppendLog($"총 {cnt}개의 작업을 완료했습니다.", LogLevel.Ok);
            }
        }
    }

    private void CancelRemainingItems()
    {
        foreach (var remainingItem in FileItems.Where(i => i.Status == "대기중" || i.Status == "변환중"))
        {
            remainingItem.Status = "취소";
            remainingItem.Progress = 0;
        }
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        var opts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*.*", opts))
                    yield return f;
            else if (File.Exists(path))
                yield return path;
        }
    }

    private void AppendLog(string msg, LogLevel level = LogLevel.Info, string titleId = "") => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));

    private void ClearLog() => Application.Current.Dispatcher.Invoke(() => LogEntries.Clear());
}