using Common;
using Common.WPF.ViewModels;
using NSW.Core;
using NSW.WPF.Services;
using RomForge.Core;
using RomForge.Core.Services.Switch;
using RomForge.Helpers;
using RomForge.Models;
using RomZip.Core.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;



namespace RomForge.ViewModels.Switch;

public class ConverterMainViewModel : ToolTabViewModel
{
    #region Fields
    private readonly Core.AppConfig _config;
    private CancellationTokenSource _cts = new();

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".nsp", ".xci", ".nsz", ".xcz" };

    #endregion

    #region Collections

    public ObservableCollection<LogEntry> LogEntries { get; } = [];
    public ObservableCollection<ConverterFileItem> FileItems { get; } = [];

    #endregion

    #region Properties

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region Commands

    public ICommand RunCommand { get; }

    #endregion

    public event Action<ConverterFileItem>? ScrollToItemRequested;

    #region Constructor

    public ConverterMainViewModel(Core.AppConfig config)
    {
        _config = config;
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsLocked && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsLocked);
    }

    #endregion

    #region Public Methods

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
                FileType = keySet == null ? "키 없음" : "분석중",
                No = FileItems.Count + 1
            };
            FileItems.Add(item);
            OnPropertyChanged(nameof(HintVisibility));
            CommandManager.InvalidateRequerySuggested();

            if (keySet == null)
                continue;

            var info = await Task.Run(() => MetadataReader.GetGameFileInfo(keySet, path));

            if (info != null)
            {
                item.TitleName = info.TitleName;
                item.TitleID = info.TitleId;
                item.Version = info.DisplayVersion;
                item.FileType = info.Type;
                if (info.IconData != null)
                    item.Icon = info.IconData.ToBitmapImage();
            }

            if (string.IsNullOrEmpty(item.TitleName))
                item.TitleName = Path.GetFileNameWithoutExtension(path);
        }
    }

    public void RemoveItems(IEnumerable<ConverterFileItem> items)
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
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        ClearLog();

        using (BeginWork())
        {
            try
            {
                var keySet = KeySetProvider.Instance.KeySet;
                if (keySet == null)
                {
                    AppendLog("키 파일이 없습니다. 설정에서 키를 먼저 등록해 주세요.", LogLevel.Error);
                    return;
                }

                int totalCount = FileItems.Count;
                AppendLog($"총 {totalCount}개의 Switch 변환 작업을 시작합니다.", LogLevel.Highlight);

                int cnt = 0;

                foreach (var item in FileItems)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    if (item.Status == "완료" || item.Status == "미지원")
                        continue;

                    item.Status = "변환중";
                    item.Progress = 0;

                    ScrollToItemRequested?.Invoke(item);

                    var progress = new Progress<ProgressInfo>(p => item.Progress = p.Percent);
                    void Log(string msg, LogLevel level, string id) => AppendLog(msg, level);

                    try
                    {
                        await ConvertItemAsync(item, progress, Log, _cts.Token);

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

                AppendLog(
                    cnt > 0
                        ? $"총 {cnt}개의 작업을 성공적으로 완료했습니다."
                        : "성공한 작업이 없습니다.",
                    cnt > 0 ? LogLevel.Ok : LogLevel.Error);
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status == "변환중"))
                    item.Status = "취소";
            }
            catch (Exception ex)
            {
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status == "변환중"))
                    item.Status = "실패";
            }
        }
    }

    private static Task ConvertItemAsync(
            ConverterFileItem item,
            IProgress<ProgressInfo> progress,
            Action<string, LogLevel, string> log,
            CancellationToken ct)
    {
        string source = item.Extension.ToLower();
        string target = item.SelectedTargetFormat.ToLower();

        return (source, target) switch
        {
            ("nsp", "xci") => NspXciConvertService.NspToXciAsync(item.FilePath, progress, log, ct),
            ("nsp", "nsz") => NspCompressService.CompressAsync(item.FilePath, 18, false, false, progress, log, ct),
            ("nsp", "xcz") => NspToXczAsync(item.FilePath, progress, log, ct),
            ("xci", "nsp") => NspXciConvertService.XciToNspAsync(item.FilePath, progress, log, ct),
            ("xci", "xcz") => XciCompressService.CompressAsync(item.FilePath, 18, false, false, progress, log, ct),
            ("xci", "nsz") => XciToNszAsync(item.FilePath, progress, log, ct),
            ("nsz", "nsp") => NspCompressService.DecompressAsync(item.FilePath, progress, log, ct),
            ("nsz", "xci") => NszToXciAsync(item.FilePath, progress, log, ct),
            ("nsz", "xcz") => NszToXczAsync(item.FilePath, progress, log, ct),
            ("xcz", "xci") => XciCompressService.DecompressAsync(item.FilePath, progress, log, ct),
            ("xcz", "nsp") => XczToNspAsync(item.FilePath, progress, log, ct),
            ("xcz", "nsz") => XczToNszAsync(item.FilePath, progress, log, ct),
            _ => Task.FromException(new NotSupportedException($"{source} → {target}: 지원하지 않는 변환입니다."))
        };
    }

    // NSP → XCZ: NSP → XCI → XCZ 2단계
    private static async Task NspToXczAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        string xciPath = await NspXciConvertService.NspToXciAsync(inputPath, progress, log, ct);
        try
        {
            await XciCompressService.CompressAsync(xciPath, 18, false, false, progress, log, ct);
        }
        finally
        {
            if (File.Exists(xciPath)) File.Delete(xciPath);
        }
    }

    // XCI → NSZ: XCI → NSP → NSZ 2단계
    private static async Task XciToNszAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        string nspPath = await NspXciConvertService.XciToNspAsync(inputPath, progress, log, ct);
        try
        {
            await NspCompressService.CompressAsync(nspPath, 18, false, false, progress, log, ct);
        }
        finally
        {
            if (File.Exists(nspPath)) File.Delete(nspPath);
        }
    }

    // NSZ → XCI: NSZ → NSP → XCI 2단계
    private static async Task NszToXciAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        string nspPath = await NspCompressService.DecompressAsync(inputPath, progress, log, ct);
        try
        {
            await NspXciConvertService.NspToXciAsync(nspPath, progress, log, ct);
        }
        finally
        {
            if (File.Exists(nspPath)) File.Delete(nspPath);
        }
    }

    // NSZ → XCZ: NSZ → NSP → XCZ 2단계
    private static async Task NszToXczAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        string nspPath = await NspCompressService.DecompressAsync(inputPath, progress, log, ct);
        try
        {
            await XciCompressService.CompressAsync(nspPath, 18, false, false, progress, log, ct);
        }
        finally
        {
            if (File.Exists(nspPath)) File.Delete(nspPath);
        }
    }

    // XCZ → NSP: XCZ → XCI → NSP 2단계
    private static async Task XczToNspAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        string xciPath = await XciCompressService.DecompressAsync(inputPath, progress, log, ct);
        try
        {
            await NspXciConvertService.XciToNspAsync(xciPath, progress, log, ct);
        }
        finally
        {
            if (File.Exists(xciPath)) File.Delete(xciPath);
        }
    }

    // XCZ → NSZ: XCZ → XCI → NSZ 2단계
    private static async Task XczToNszAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        string xciPath = await XciCompressService.DecompressAsync(inputPath, progress, log, ct);
        try
        {
            await NspCompressService.CompressAsync(xciPath, 18, false, false, progress, log, ct);
        }
        finally
        {
            if (File.Exists(xciPath)) File.Delete(xciPath);
        }
    }

    private void AppendLog(string msg, LogLevel level = LogLevel.Info)
    {
        if (Application.Current?.Dispatcher == null) return;
        Application.Current.Dispatcher.Invoke(() =>
            LogEntries.Add(new LogEntry { Message = msg, Level = level }));
    }

    private void ClearLog()
    {
        if (Application.Current?.Dispatcher == null) return;
        Application.Current.Dispatcher.Invoke(() => LogEntries.Clear());
    }

    #endregion
}