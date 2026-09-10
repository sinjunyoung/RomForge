using _3DS.Core.Crypto;
using _3DS.Core.Models;
using _3DS.Core.Services;
using CD.Core.Services.Readers;
using CD.Core.Services.Writers;
using CHD.Core.Services;
using Common;
using Common.WPF.ViewModels;
using DolphinTool.Core.Services;
using PBP.Core.Enums;
using PBP.Core.Services;
using PSP.Core.Models;
using PSP.Core.Services;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Models._3DS;
using RomForge.Core.Models.CD;
using RomForge.Core.Models.Compression;
using RomForge.Core.Models.PS;
using RomForge.Core.Models.Switch;
using RomForge.Core.Models.WiiU;
using RomForge.Core.Services.Compression;
using RomForge.Core.Services.PS;
using RomForge.Core.Services.Switch;
using RomForge.Core.Services.WiiU;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WiiU.Core.Services;

namespace RomForge.ViewModels;

public class ConvertMainViewModel : ToolTabViewModel
{
    private CancellationTokenSource _cts = new();

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nsp", ".xci", ".nsz", ".xcz",
        ".cci", ".cia", ".3ds", ".zcci",
        ".wud", ".wux", ".wua",
        ".mds", ".ccd",
        ".pbp",
        ".iso", ".cue", ".gdi", ".chd",
        ".gcm", ".wbfs", ".gcz", ".wia", ".rvz",
        ".cso", ".zso",
    };

    private static string KeysPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys.txt");

    public ObservableCollection<LogEntry> LogEntries { get; } = [];
    public ObservableCollection<object> FileItems { get; } = [];

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }

    public event Action<object>? ScrollToItemRequested;
    public event EventHandler? RunNavigateCerts;

    public ConvertMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsLocked && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsLocked);
    }

    public async Task AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems
            .Select(GetFilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newItems = new List<object>();

        foreach (var path in ExpandPaths(paths))
        {
            if (!existing.Add(path))
                continue;

            var item = CreateItem(path);

            if (item is null)
                continue;

            FileItems.Add(item);
            newItems.Add(item);
        }

        Renumber();
        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();

        foreach (var item in newItems)
            await ProbeMetadataAsync(item);
    }

    public void RemoveItems(IEnumerable<object> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        Renumber();
        OnPropertyChanged(nameof(HintVisibility));
    }

    public void ClearItems()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
    }

    private void Renumber()
    {
        for (int i = 0; i < FileItems.Count; i++)
            ((IProgressTrackable)FileItems[i]).No = i + 1;
    }

    private static string GetFilePath(object item) => item switch
    {
        FileItemBase f => f.FilePath,
        _ => string.Empty
    };

    private static object? CreateItem(string path)
    {
        var ext = Directory.Exists(path) ? "" : Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

        switch (ext)
        {
            case "nsp":
            case "xci":
            case "nsz":
            case "xcz":
                var sw = new ConverterFileItem(path);
                return sw.SelectedTargetFormat == "" ? null : sw;

            case "cci":
            case "3ds":
            case "cia":
            case "zcci":
                var ds = new _3DSFileItem(path);
                return ds.SelectedTargetFormat is "" or "미지원" ? null : ds;

            case "wud":
            case "wux":
            case "wua":
                return new WiiUFileItem(path);

            case "mds":
            case "ccd":
                return new CdConvertFileItem(path);

            case "pbp":
                return new PbpFileItem(path);

            case "iso":
            case "cue":
            case "gdi":
            case "chd":
            case "gcm":
            case "wbfs":
            case "gcz":
            case "wia":
            case "rvz":
            case "cso":
            case "zso":
                var disc = new DiscConvertFileItem(path);
                return disc.AvailableFormats.Count == 0 ? null : disc;

            default:
                if (Directory.Exists(path))
                {
                    var wiiu = new WiiUFileItem(path);
                    return wiiu.SelectedTargetFormat == "미지원" ? null : wiiu;
                }

                return null;
        }
    }

    private static async Task ProbeMetadataAsync(object item)
    {
        try
        {
            switch (item)
            {
                case _3DSFileItem ds:
                    {
                        var result = await Task.Run(() => Core.Services._3DS.Util.ParseFile(ds.FilePath));

                        ds.TitleId = result.Title!.TitleId;
                        ds.ProductCode = result.ProductCode;
                        ds.ShortDescription = result.ShortDescription;
                        ds.Publisher = result.Publisher;
                        ds.Crypto = result.Crypto;

                        if (result.IconPixels is not null)
                        {
                            var bitmap = BitmapSource.Create(48, 48, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null, result.IconPixels, 48 * 4);
                            bitmap.Freeze();
                            ds.Icon = bitmap;
                        }
                    }
                    break;

                case WiiUFileItem wu:
                    await Task.Run(() =>
                    {
                        if (Directory.Exists(wu.FilePath))
                        {
                            using ITitleSource source = wu.Extension == "wup"
                                ? new WupTitleSource(wu.FilePath)
                                : new FolderTitleSource(wu.FilePath);

                            wu.TitleIdHex = source.TitleIdHex;
                            wu.TitleVersion = source.TitleVersion;

                            var folderMeta = wu.Extension == "wup"
                                ? WiiUMetadataExtractor.ExtractFromTitleSource(source)
                                : WiiUMetadataExtractor.ExtractFromFolder(wu.FilePath);

                            if (folderMeta is not null)
                                wu.TitleName = folderMeta.Title;
                        }
                    });

                    if (!Directory.Exists(wu.FilePath))
                    {
                        var meta = await WiiUMetadataExtractor.Extract(wu.FilePath, KeysPath);

                        if (meta is not null)
                            wu.TitleName = meta.Title;
                    }

                    break;

                case CdConvertFileItem cd:
                    var trackCount = await Task.Run(() =>
                    {
                        var reader = DiscImageReaderFactory.Resolve(cd.FilePath);
                        return reader.Read(cd.FilePath).TrackCount;
                    });

                    cd.TrackCount = trackCount;
                    break;

                case PbpFileItem pbp:
                    await Task.Run(() =>
                    {
                        using var stream = new FileStream(pbp.FilePath, FileMode.Open, FileAccess.Read);
                        var reader = new PbpReader(stream);
                        var meta = GameMetadataLookup.Find(reader.Discs[0].DiscID);

                        pbp.TitleName = meta?.ETitle ?? string.Empty;
                        pbp.TitleLocalName = meta?.LTitle ?? string.Empty;
                        pbp.Languages = meta?.Languages ?? [];
                        pbp.TitleId = string.Join(", ", reader.Discs.Select(d => d.DiscID));

                        if (PbpReader.TryGetResourceStream(ResourceType.ICON0, stream, out var iconStream))
                        {
                            var bitmap = new BitmapImage();

                            bitmap.BeginInit();
                            bitmap.StreamSource = iconStream;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();

                            pbp.Icon = bitmap;
                        }
                    });
                    break;
            }
        }
        catch
        {
        }
    }

    private async Task RunAsync()
    {
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        ClearLog();

        using (BeginWork())
        {
            try
            {
                int cnt = 0;

                AppendLog($"총 {FileItems.Count}개의 통합 변환 작업을 시작합니다.", LogLevel.Highlight);

                foreach (var item in FileItems.ToList())
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    var tracker = (IProgressTrackable)item;

                    if (tracker.Status is "완료" or "미지원")
                        continue;

                    tracker.Status = "대기중";
                    tracker.Progress = 0;
                    tracker.Status = "변환중";

                    ScrollToItemRequested?.Invoke(item);

                    try
                    {
                        await ConvertOneAsync(item);

                        tracker.Progress = 100;
                        tracker.Status = "완료";
                        cnt++;
                    }
                    catch (CertsBinNotFoundException e)
                    {
                        AppendLog(e.Message, LogLevel.Error);
                        RunNavigateCerts?.Invoke(this, EventArgs.Empty);
                        tracker.Status = "실패";
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"[{GetDisplayName(item)}] 변환 실패: {ex.Message}", LogLevel.Error);
                        tracker.Status = "실패";
                        tracker.Progress = 0;
                    }
                }

                AppendLog(cnt > 0 ? $"총 {cnt}개의 작업을 성공적으로 완료했습니다." : "성공한 작업이 없습니다.", cnt > 0 ? LogLevel.Ok : LogLevel.Error);
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);

                foreach (var item in FileItems)
                {
                    var tracker = (IProgressTrackable)item;

                    if (tracker.Status is "대기중" or "변환중")
                        tracker.Status = "취소";
                }
            }
            finally
            {
            }
        }
    }

    private async Task ConvertOneAsync(object item)
    {
        var progress = new Progress<ProgressInfo>(p => ((IProgressTrackable)item).Progress = p.Percent);
        void Log(string msg, LogLevel level, string id = "") => AppendLog(msg, level);

        switch (item)
        {
            case ConverterFileItem sw:
                {
                    int compressLevel = GetSwitchCompressLevel();

                    switch (sw.Extension.ToLowerInvariant(), sw.SelectedTargetFormat.ToUpperInvariant())
                    {
                        case ("nsp", "XCI"):
                            await NspXciConvertService.NspToXciAsync(sw.FilePath, progress, Log, _cts.Token);
                            break;
                        case ("nsp", "NSZ"):
                            await NspCompressService.CompressAsync(sw.FilePath, compressLevel, AppConfig.Instance.Switch.VerifyCompress, AppConfig.Instance.Switch.UseBlockMode, progress, Log, _cts.Token);
                            break;
                        case ("nsp", "XCZ"):
                            await NspXciConvertService.NspToXczAsync(sw.FilePath, compressLevel, AppConfig.Instance.Switch.VerifyCompress, AppConfig.Instance.Switch.UseBlockMode, progress, Log, _cts.Token);
                            break;
                        case ("xci", "NSP"):
                            await NspXciConvertService.XciToNspAsync(sw.FilePath, progress, Log, _cts.Token);
                            break;
                        case ("xci", "XCZ"):
                            await XciCompressService.CompressAsync(sw.FilePath, compressLevel, AppConfig.Instance.Switch.VerifyCompress, AppConfig.Instance.Switch.UseBlockMode, progress, Log, _cts.Token);
                            break;
                        case ("xci", "NSZ"):
                            await NspXciConvertService.XciToNszAsync(sw.FilePath, compressLevel, AppConfig.Instance.Switch.VerifyCompress, AppConfig.Instance.Switch.UseBlockMode, progress, Log, _cts.Token);
                            break;
                        case ("nsz", "NSP"):
                            await NspCompressService.DecompressAsync(sw.FilePath, progress, Log, _cts.Token);
                            break;
                        case ("nsz", "XCI"):
                            await NspXciConvertService.NszToXciAsync(sw.FilePath, progress, Log, _cts.Token);
                            break;
                        case ("nsz", "XCZ"):
                            await NspXciConvertService.NszToXczAsync(sw.FilePath, compressLevel, AppConfig.Instance.Switch.VerifyCompress, AppConfig.Instance.Switch.UseBlockMode, progress, Log, _cts.Token);
                            break;
                        case ("xcz", "XCI"):
                            await XciCompressService.DecompressAsync(sw.FilePath, progress, Log, _cts.Token);
                            break;
                        case ("xcz", "NSP"):
                            await NspXciConvertService.XczToNspAsync(sw.FilePath, progress, Log, _cts.Token);
                            break;
                        case ("xcz", "NSZ"):
                            await NspXciConvertService.XczToNszAsync(sw.FilePath, compressLevel, AppConfig.Instance.Switch.VerifyCompress, AppConfig.Instance.Switch.UseBlockMode, progress, Log, _cts.Token);
                            break;
                        default:
                            throw new NotSupportedException($"{sw.Extension} → {sw.SelectedTargetFormat}: 지원하지 않는 변환입니다.");
                    }
                }
                break;

            case _3DSFileItem ds:
                {
                    KeyStore key = new();

                    switch (ds.Extension.ToLowerInvariant(), ds.SelectedTargetFormat.ToUpperInvariant())
                    {
                        case ("cci", "CIA") or ("3ds", "CIA"):
                            await new CciToCiaConverter(key).ConvertAsync(ds.FilePath, progress, AppendLog, _cts.Token);
                            break;
                        case ("cia", "CCI"):
                            await new CiaToCciConverter(key).ConvertAsync(ds.FilePath, progress, AppendLog, _cts.Token);
                            break;
                        case ("cci", "ZCCI") or ("3ds", "ZCCI"):
                            await Z3dsArchiveService.CompressAsync(ds.FilePath, AppConfig.Instance.Azahar.CompressLevel, progress, AppendLog, _cts.Token);
                            break;
                        case ("cia", "ZCCI"):
                            await Z3dsArchiveService.CompressFromCiaAsync(ds.FilePath, AppConfig.Instance.Azahar.CompressLevel, progress, AppendLog, _cts.Token);
                            break;
                        case ("zcci", "CCI"):
                            await Z3dsArchiveService.DecompressAsync(ds.FilePath, progress, AppendLog, _cts.Token);
                            break;
                        case ("zcci", "CIA"):
                            await new CciToCiaConverter(key).ConvertAsync(ds.FilePath, progress, AppendLog, _cts.Token);
                            break;
                        default:
                            throw new NotSupportedException($"{ds.Extension} → {ds.SelectedTargetFormat}: 지원하지 않는 변환입니다.");
                    }
                }
                break;

            case WiiUFileItem wu:
                await Task.Run(() => ConvertWiiUOne(wu, _cts.Token));
                break;

            case CdConvertFileItem cd:
                await ConvertCdOneAsync(cd, progress, _cts.Token);
                break;

            case DiscConvertFileItem disc:
                await ConvertDiscOneAsync(disc, progress, _cts.Token);
                break;

            case PbpFileItem pbp:
                {
                    var unpacker = new PbpUnpacker
                    {
                        OnNotify = msg => AppendLog(msg),
                        OnProgress = percent => ((IProgressTrackable)pbp).Progress = percent
                    };

                    await unpacker.UnpackAsync(pbp.FilePath, ResolveOutputDir(pbp.FilePath), true, _cts.Token);
                }
                break;
        }
    }

    private static int GetSwitchCompressLevel()
    {
        int level = AppConfig.Instance.Switch.CompressLevel;
        return level < 3 ? 3 : level;
    }

    private async Task ConvertCdOneAsync(CdConvertFileItem cd, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        if (cd.SelectedTargetFormat != "CHD")
        {
            var reader = DiscImageReaderFactory.Resolve(cd.FilePath);
            var discImage = reader.Read(cd.FilePath);
            var outDir = ResolveOutputDir(cd.FilePath);

            if (cd.OutputFormat == CdOutputFormat.Iso)
                await IsoWriter.WriteAsync(discImage, outDir, cd.FileName, progress, ct);
            else
                await BinCueWriter.WriteAsync(discImage, outDir, cd.FileName, progress, ct);

            return;
        }

        var ccdReader = DiscImageReaderFactory.Resolve(cd.FilePath);
        var ccdImage = ccdReader.Read(cd.FilePath);
        var tempCuePath = await BinCueWriter.WriteAsync(ccdImage, cd.Directory, cd.FileName, progress, ct);
        var tempBinPath = Path.ChangeExtension(tempCuePath, ".bin");

        try
        {
            FileConverter chdFromCcd = new(AppConfig.Instance.Chdman.Compression);

            chdFromCcd.LogMessage += (_, e) => AppendLog(e.Message, e.Level);

            var chdFromCcdResult = await chdFromCcd.ConvertFileAsync(tempCuePath, null, progress, ct);

            if (!chdFromCcdResult.Success)
                throw new InvalidOperationException(chdFromCcdResult.Message);
        }
        finally
        {
            if (File.Exists(tempCuePath))
                File.Delete(tempCuePath);

            if (File.Exists(tempBinPath))
                File.Delete(tempBinPath);
        }
    }

    private async Task ConvertDiscOneAsync(DiscConvertFileItem item, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        string ext = item.Extension.ToLowerInvariant();
        string target = item.SelectedTargetFormat.ToUpperInvariant();

        if (item.DetectedFormat is RomFormat.Gcm or RomFormat.Wbfs or RomFormat.Gcz or RomFormat.Wia or RomFormat.Rvz or RomFormat.Wii)
        {
            var detected = FormatDetector.Detect(item.FilePath);

            DolphinService dolphin = new();

            dolphin.LogMessage += (_, e) => AppendLog(e.Message, e.Level);
            dolphin.ProgressChanged += (_, e) => Application.Current.Dispatcher.Invoke(() => item.Progress = e.Progress);

            await dolphin.ConvertFileAsync(item.FilePath, detected.Format.ToString(), detected.OutputExtension, AppConfig.Instance.Dolphin.CompressLevel, null, ct);

            return;
        }

        if (ext is "cso" or "zso")
        {
            string outPath = Utils.GetUniqueFilePath(Path.ChangeExtension(item.FilePath, target.ToLowerInvariant()));

            CsoService csoService = new();

            switch (ext, target)
            {
                case ("cso", "ISO") or ("zso", "ISO"):
                    await using (var input = File.OpenRead(item.FilePath))
                    await using (var output = File.Create(outPath))
                        await CsoService.DecompressAsync(input, output, progress, ct);
                    break;

                case ("cso", "ZSO"):
                    await using (var input = File.OpenRead(item.FilePath))
                    await using (var output = File.Create(outPath))
                        await CsoService.TranscodeAsync(input, output, targetMagic: CsoHeader.MagicZSO, targetIsLz4: true, progress: progress, ct: ct);
                    break;

                case ("zso", "CSO"):
                    await using (var input = File.OpenRead(item.FilePath))
                    await using (var output = File.Create(outPath))
                        await CsoService.TranscodeAsync(input, output, targetMagic: CsoHeader.MagicCSO, targetIsLz4: false, progress: progress, ct: ct);
                    break;

                case ("cso", "CHD") or ("zso", "CHD"):
                    await csoService.CompressCsoToChdAsync(item.FilePath, outPath, progress, AppConfig.Instance.Chdman.Compression, ct);
                    break;

                default:
                    throw new NotSupportedException($"{ext} → {target}: 지원하지 않는 변환입니다.");
            }

            return;
        }

        if (target is "CSO" or "ZSO")
        {
            string outPath = Utils.GetUniqueFilePath(Path.ChangeExtension(item.FilePath, target.ToLowerInvariant()));

            if (ext == "chd")
            {
                await using var output = File.Create(outPath);

                if (target == "CSO")
                    await CsoService.CompressFromChdAsync(item.FilePath, output, version: 1, progress: progress, ct: ct);
                else
                    await CsoService.CompressFromChdAsync(item.FilePath, output, magic: CsoHeader.MagicZSO, isLz4: true, progress: progress, ct: ct);
            }
            else
            {
                await using var input = File.OpenRead(item.FilePath);
                await using var output = File.Create(outPath);

                if (target == "CSO")
                    await CsoService.CompressAsync(input, output, progress: progress, ct: ct);
                else
                    await CsoService.CompressAsync(input, output, magic: CsoHeader.MagicZSO, isLz4: true, progress: progress, ct: ct);
            }

            return;
        }

        FileConverter converter = new(AppConfig.Instance.Chdman.Compression);

        converter.LogMessage += (_, e) => AppendLog(e.Message, e.Level);

        var result = await converter.ConvertFileAsync(item.FilePath, null, progress, ct);

        if (!result.Success)
            throw new InvalidOperationException(result.Message);
    }

    private void ConvertWiiUOne(WiiUFileItem item, CancellationToken ct)
    {
        var sources = WiiUConverter.OpenSources(item.FilePath, KeysPath);
        var outputRoot = ResolveOutputDir(item.FilePath);

        try
        {
            int total = sources.Count;

            for (int i = 0; i < sources.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var source = sources[i];
                var outputName = WiiUConverter.BuildOutputName(source, item.TitleName);

                void OnFileProgress(int done, int totalFiles, string label)
                {
                    int subPercent = totalFiles > 0 ? (int)(done * 100.0 / totalFiles) : 100;
                    item.Progress = (int)(((i * 100.0) + subPercent) / total);
                }

                switch (item.SelectedTargetFormat)
                {
                    case "WUP":
                        var wupFolder = Utils.GetUniqueFolderPath(Path.Combine(outputRoot, $"{outputName} [WUP]"));
                        WiiUConverter.ConvertToWup(source, wupFolder, OnFileProgress, ct);
                        break;

                    case "Loadiine":
                        var loadiineFolder = Utils.GetUniqueFolderPath(Path.Combine(outputRoot, $"{outputName} [Loadiine]"));
                        WiiUConverter.ConvertToLoadiine(source, loadiineFolder, OnFileProgress, ct);
                        break;

                    case "WUA":
                        var wuaFile = Utils.GetUniqueFilePath(Path.Combine(outputRoot, $"{outputName}.wua"));
                        WiiUConverter.ConvertToWua(source, wuaFile, OnFileProgress, ct);
                        break;

                    default:
                        throw new NotSupportedException($"지원하지 않는 출력 포맷입니다: {item.SelectedTargetFormat}");
                }
            }
        }
        finally
        {
            foreach (var s in sources)
                s.Dispose();
        }
    }
    private static string ResolveOutputDir(string sourcePath) => Path.GetDirectoryName(sourcePath)!;

    private static string GetDisplayName(object item) => item switch
    {
        FileItemBase f => f.FileName,
        _ => "알 수 없음"
    };

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                if (WupTitleSource.LooksLikeWupFolder(path) || LooksLikeLoadiineFolder(path))
                {
                    yield return path;
                    continue;
                }

                var opts = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                };

                foreach (var f in Directory.EnumerateFiles(path, "*.*", opts))
                    if (SupportedExtensions.Contains(Path.GetExtension(f)))
                        yield return f;
            }
            else if (File.Exists(path) && SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                yield return path;
            }
        }
    }

    private static bool LooksLikeLoadiineFolder(string path) =>
        Directory.Exists(Path.Combine(path, "code")) && Directory.Exists(Path.Combine(path, "content")) && Directory.Exists(Path.Combine(path, "meta"));

    private void AppendLog(string msg, LogLevel level = LogLevel.Info) => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));

    private void ClearLog() => Application.Current.Dispatcher.Invoke(() => LogEntries.Clear());
}