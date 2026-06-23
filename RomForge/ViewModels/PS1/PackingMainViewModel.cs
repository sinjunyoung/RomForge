using Common;
using Common.WPF.ViewModels;
using NSW.WPF.Services;
using PBP.Core.Models;
using PBP.Core.Services;
using RomForge.Core;
using RomForge.Core.Services.PS1;
using RomForge.Helpers;
using RomForge.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels.PS1;

public class PackingMainViewModel : ToolTabViewModel
{
    private const int MaxItems = 5;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cue", ".m3u", ".iso", ".chd"
    };

    private CancellationTokenSource _cts = new();
    private string? _lastIconGameId;
    private CancellationTokenSource? _iconCts;

    private string _gameTitle = string.Empty;
    public string GameTitle
    {
        get => _gameTitle;
        set { _gameTitle = value; OnPropertyChanged(); }
    }

    private int _progressPct;
    private string _progressLabel = string.Empty;
    private string _progressPercent = "0%";
    private string _progressTime = string.Empty;
    private string _progressSpeed = string.Empty;
    private readonly AppConfig _config;

    public int ProgressPct
    {
        get => _progressPct;
        set { _progressPct = value; OnPropertyChanged(); }
    }

    public string ProgressLabel
    {
        get => _progressLabel;
        set { _progressLabel = value; OnPropertyChanged(); }
    }

    public string ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); }
    }

    public string ProgressTime
    {
        get => _progressTime;
        set { _progressTime = value; OnPropertyChanged(); }
    }

    public string ProgressSpeed
    {
        get => _progressSpeed;
        set { _progressSpeed = value; OnPropertyChanged(); }
    }

    public bool CanAdd => !IsLocked && FileItems.Count < MaxItems;

    private BitmapImage? _icon0Image;
    public BitmapImage? Icon0Image { get => _icon0Image; set { _icon0Image = value; OnPropertyChanged(); } }

    private BitmapImage? _pic0Image;
    public BitmapImage? Pic0Image { get => _pic0Image; set { _pic0Image = value; OnPropertyChanged(); } }

    private BitmapImage? _pic1Image;
    public BitmapImage? Pic1Image { get => _pic1Image; set { _pic1Image = value; OnPropertyChanged(); } }

    private byte[] _icon0Bytes = PbpResources.ICON0;
    private byte[] _pic0Bytes = PbpResources.PIC0;
    private byte[] _pic1Bytes = PbpResources.PIC1;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ObservableCollection<DiscFileItem> FileItems { get; } = [];

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public byte[] Icon0Bytes { get => _icon0Bytes; set => _icon0Bytes = value; }
    public byte[] Pic0Bytes { get => _pic0Bytes; set => _pic0Bytes = value; }
    public byte[] Pic1Bytes { get => _pic1Bytes; set => _pic1Bytes = value; }

    public PackingMainViewModel(AppConfig config)
    {
        _config = config;
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsLocked && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsLocked);

        Icon0Image = Icon0Bytes.ToBitmapImage();
        Pic0Image = Pic0Bytes.ToBitmapImage();
        Pic1Image = Pic1Bytes.ToBitmapImage();

        FileItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(CanAdd));

        PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(IsLocked))
                OnPropertyChanged(nameof(CanAdd));
        };
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var inputPaths = paths.ToList();

        if (inputPaths.Count == 0) 
            return;

        var rawFiles = ExpandPaths(inputPaths).ToList();
        var explodedPaths = new List<string>();

        foreach (var file in rawFiles)
        {
            if (Path.GetExtension(file).Equals(".m3u", StringComparison.OrdinalIgnoreCase))
                explodedPaths.AddRange(ResolveM3uAllDiscs(file));
            else
                explodedPaths.Add(file);
        }

        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newCandidates = new List<string>();

        foreach (var p in explodedPaths)
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(p))) 
                continue;

            if (existing.Add(p))
                newCandidates.Add(p);
        }

        var room = MaxItems - FileItems.Count;
        var toAdd = newCandidates.Take(Math.Max(room, 0)).ToList();

        foreach (var path in toAdd)
        {
            var item = new DiscFileItem(path);
            FileItems.Add(item);
            _ = LoadItemInfoAsync(item);
        }

        int userRejectedCount = newCandidates.Count - toAdd.Count;

        if (userRejectedCount > 0)
            AppendLog($"최대 {MaxItems}개까지만 등록할 수 있습니다. {userRejectedCount}개 파일이 제외되었습니다.", LogLevel.Error);

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    private static List<string> ResolveM3uAllDiscs(string m3uPath)
    {
        var dir = Path.GetDirectoryName(m3uPath)!;
        var paths = new List<string>();

        if (!File.Exists(m3uPath)) 
            return paths;

        var lines = File.ReadAllLines(m3uPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'));

        foreach (var line in lines)
        {
            var fullPath = Path.IsPathRooted(line) ? line : Path.Combine(dir, line);
            paths.Add(fullPath);
        }

        return paths;
    }

    public void RemoveItems(IEnumerable<DiscFileItem> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        OnPropertyChanged(nameof(HintVisibility));
        ResortAndRenumber();
    }

    public void ClearItems()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
        _lastIconGameId = null;
        Icon0Image = PbpResources.ICON0.ToBitmapImage();
        Pic0Image = PbpResources.PIC0.ToBitmapImage();
        Pic1Image = PbpResources.PIC1.ToBitmapImage();
    }

    public void SetIcon0FromBytes(byte[] rawBytes) => SetImage(rawBytes, (bytes, img) => { Icon0Bytes = bytes; Icon0Image = img; }, 80, 80);
    public void SetPic0FromBytes(byte[] rawBytes) => SetImage(rawBytes, (bytes, img) => { Pic0Bytes = bytes; Pic0Image = img; }, 270, 150);
    public void SetPic1FromBytes(byte[] rawBytes) => SetImage(rawBytes, (bytes, img) => { Pic1Bytes = bytes; Pic1Image = img; }, 480, 272);

    private static void SetImage(byte[] rawBytes, Action<byte[], BitmapImage> apply, int targetWidth, int targetHeight)
    {
        using var image = Image.Load<SixLabors.ImageSharp.PixelFormats.Bgra32>(rawBytes);
        image.Mutate(x => x.Resize(targetWidth, targetHeight));

        using var ms = new MemoryStream();
        image.SaveAsPng(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder
        {
            CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression
        });
        byte[] finalBytes = ms.ToArray();

        ms.Position = 0;
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = ms;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        apply(finalBytes, bitmapImage);
    }

    private async Task LoadItemInfoAsync(DiscFileItem item)
    {
        try
        {
            var (gameId, size) = await Task.Run(() =>
            {
                var ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
                var size = DiscSizeResolver.GetTotalSize(item.FilePath);

                if (ext == ".chd")
                    return (GameIdReader.ReadFromDisk(DiskSource.FromChd(item.FilePath)), size);

                if (ext == ".cue")
                {
                    var cueFile = CueFileReader.Read(item.FilePath);
                    var binPath = CueFileResolver.GetBinPath(item.FilePath);
                    using var stream = new FileStream(binPath, FileMode.Open, FileAccess.Read);

                    return (GameIdReader.ReadFromStream(stream, stream.Length), size);
                }

                using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read);

                return (GameIdReader.ReadFromStream(fs, fs.Length), size);
            });

            item.GameId = gameId;
            item.FileSizeBytes = size;
        }
        catch (Exception ex)
        {
            item.GameId = "인식실패";
            AppendLog($"[{item.FileName}] GameID 인식 실패: {ex.Message}", LogLevel.Error);
        }

        ResortAndRenumber();
    }

    private void ResortAndRenumber()
    {
        var sorted = FileItems
            .OrderBy(i => i.GameId is "인식중..." or "인식실패" ? 1 : 0)
            .ThenBy(i => i.GameId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].No = i + 1;
            var oldIndex = FileItems.IndexOf(sorted[i]);

            if (oldIndex != i)
                FileItems.Move(oldIndex, i);
        }

        _ = UpdateImageAsync();
    }

    private async Task UpdateImageAsync()
    {
        try
        {
            var primary = FileItems.FirstOrDefault(i => i.No == 1);

            if (primary == null || primary.GameId == _lastIconGameId || primary.GameId is "인식중..." or "인식실패")
                return;

            var meta = GameMetadataLookup.Find(primary.GameId);

            if (meta != null && !string.IsNullOrWhiteSpace(meta.Title))
                GameTitle = meta.Title;

            _lastIconGameId = primary.GameId;

            var old = Interlocked.Exchange(ref _iconCts, new CancellationTokenSource());
            old?.Cancel();
            old?.Dispose();

            var ct = _iconCts.Token;

            var icon0Png = await CoverArtFetcher.TryDownloadIconPngAsync(primary.GameId, ct);
            ct.ThrowIfCancellationRequested();
            Icon0Bytes = icon0Png ?? PbpResources.ICON0;
            Icon0Image = Icon0Bytes.ToBitmapImage();

            var pic0Png = meta != null ? await GameMetadataLookup.TryDownloadImagePngAsync(meta.Pic0, ct) : null;
            ct.ThrowIfCancellationRequested();
            Pic0Bytes = pic0Png ?? PbpResources.PIC0;
            Pic0Image = Pic0Bytes.ToBitmapImage();

            var pic1Png = meta != null ? await GameMetadataLookup.TryDownloadImagePngAsync(meta.Pic1, ct) : null;
            ct.ThrowIfCancellationRequested();
            Pic1Bytes = pic1Png ?? PbpResources.PIC1;
            Pic1Image = Pic1Bytes.ToBitmapImage();
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunAsync()
    {
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        if (FileItems.Count == 0)
        {
            AppendLog("추가된 파일이 없습니다.", LogLevel.Error);
            return;
        }

        if (FileItems.Any(i => i.GameId is "인식중..." or "인식실패"))
        {
            AppendLog("GameID 인식 오류", LogLevel.Error);
            return;
        }

        using (BeginWork())
        {
            var orderedItems = FileItems.OrderBy(i => i.No).ToList();
            var gameTitle = string.IsNullOrWhiteSpace(GameTitle) ? GuessTitle(orderedItems[0]) : GameTitle;
            var mainGameId = orderedItems[0].GameId;

            var assets = new PbpAssets
            {
                Icon0Png = Icon0Bytes.ResizePng(80, 80),
                Pic0Png = Pic0Bytes.ResizePng(480, 272),
                Pic1Png = Pic1Bytes.ResizePng(480, 272),
                DataPsp = PbpResources.DATA
            };

            string baseDirectory = Path.GetDirectoryName(orderedItems[0].FilePath)!;

            string targetOutputPath;
            string? gameDirectory = null;

            if (_config.PS1.UseGameIdMode)
            {
                gameDirectory = Path.Combine(baseDirectory, mainGameId);
                targetOutputPath = Path.Combine(gameDirectory, "eboot.pbp");
            }
            else
            {
                var safeTitle = string.Concat(gameTitle.Split(Path.GetInvalidFileNameChars()));
                targetOutputPath = Path.Combine(baseDirectory, safeTitle + ".pbp");
            }

            var resolvedDiscs = new List<ResolvedDisc>();

            try
            {
                if (gameDirectory != null && !Directory.Exists(gameDirectory))
                    Directory.CreateDirectory(gameDirectory);

                AppendLog($"작업 시작: {gameTitle} [{mainGameId}] ({orderedItems.Count}개 디스크)", LogLevel.Highlight);

                var discInfos = new List<DiscWriteInfo>();

                foreach (var item in orderedItems)
                {
                    var resolved = RawDiscProcessor.Resolve(item.FilePath);
                    resolvedDiscs.Add(resolved);
                    discInfos.Add(new DiscWriteInfo(resolved.IsoStream, resolved.IsoLength, mainGameId, orderedItems.Count > 1 ? $"{gameTitle} - Disc {item.No}" : gameTitle, resolved.TocData));
                }

                await PbpPackager.WritePbpAsync(discInfos, mainGameId, gameTitle, targetOutputPath, _config.PS1.CompressLevel, assets, BuildProgressReporter(), _cts.Token);

                ProgressPct = 100;
                AppendLog($"작업 완료: {targetOutputPath}", LogLevel.Ok);

                Path.GetDirectoryName(targetOutputPath).OpenFolder();
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                CleanupTask();
                TryDeleteFileAndFolder(targetOutputPath, gameDirectory);
            }
            catch (Exception ex)
            {
                AppendLog($"오류: [{gameTitle}] {ex.Message}", LogLevel.Error);
                CleanupTask();
                TryDeleteFileAndFolder(targetOutputPath, gameDirectory);
            }
            finally
            {
                foreach (var d in resolvedDiscs) 
                    d.Dispose();
            }
        }
    }

    private Progress<ProgressInfo> BuildProgressReporter() =>
    new(info =>
    {
        ProgressPct = info.Percent;
        ProgressLabel = info.Label;
        ProgressPercent = $"{info.Percent}%";
        ProgressTime = info.TimeInfo;
        ProgressSpeed = info.Speed;
    });

    private void CleanupTask()
    {
        ProgressPct = 0;
        ProgressLabel = string.Empty;
        ProgressPercent = "0%";
        ProgressTime = string.Empty;
        ProgressSpeed = string.Empty;
    }

    private void TryDeleteFileAndFolder(string? filePath, string? folderPath)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                File.Delete(filePath);

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath) && !Directory.EnumerateFileSystemEntries(folderPath).Any())
                Directory.Delete(folderPath);
        }
        catch (Exception ex)
        {
            AppendLog($"취소/실패 정리 작업 중 예외 발생: {ex.Message}", LogLevel.Error);
        }
    }

    private static string GuessTitle(DiscFileItem item)
        => System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(item.FilePath), @"\s*\(Disc\s*\d+\)", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

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

    private void AppendLog(string msg, LogLevel level = LogLevel.Info)
    {
        if (Application.Current?.Dispatcher == null) 
            return;

        Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
    }

    public static string GetFileDialogFilter()
    {
        var wildcards = string.Join(";", SupportedExtensions.Select(ext => $"*{ext}"));

        return $"지원 파일|{wildcards}|모든 파일|*.*";
    }
}