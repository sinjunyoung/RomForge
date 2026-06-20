using Common;
using Common.WPF.ViewModels;
using NSW.WPF.Services;
using PBP.Core.Models;
using PBP.Core.Services;
using RomForge.Core.Services.PS1;
using RomForge.Helpers;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels.PS1;

public class ConverterMainViewModel : ToolTabViewModel
{
    private const int MaxItems = 5;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cue", ".m3u", ".iso", ".chd"
    };

    private CancellationTokenSource _cts = new();
    private string? _lastIconGameId;
    private CancellationTokenSource? _iconCts;

    private bool _isConverting;
    public bool IsConverting
    {
        get => _isConverting;
        set { _isConverting = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanAdd)); CommandManager.InvalidateRequerySuggested(); }
    }

    private string _gameTitle;
    public string GameTitle
    {
        get => _gameTitle;
        set { _gameTitle = value; OnPropertyChanged(); }
    }

    public bool CanAdd => !IsConverting && FileItems.Count < MaxItems;

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

    public ConverterMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);

        Icon0Image = _icon0Bytes.ToBitmapImage();
        Pic0Image = _pic0Bytes.ToBitmapImage();
        Pic1Image = _pic1Bytes.ToBitmapImage();

        FileItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(CanAdd));
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

    public void SetIcon0FromFile(string path) => SetImage(File.ReadAllBytes(path), bytes => { _icon0Bytes = bytes; Icon0Image = bytes.ToBitmapImage(); });
    public void SetPic0FromFile(string path) => SetImage(File.ReadAllBytes(path), bytes => { _pic0Bytes = bytes; Pic0Image = bytes.ToBitmapImage(); });
    public void SetPic1FromFile(string path) => SetImage(File.ReadAllBytes(path), bytes => { _pic1Bytes = bytes; Pic1Image = bytes.ToBitmapImage(); });

    private static void SetImage(byte[] rawBytes, Action<byte[]> apply)
    {
        try { apply(ImageConversion.ToPng(rawBytes)); }
        catch { /* 이미지가 아니면 그냥 무시 */ }
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

                using var disc = CuePreprocessor.Resolve(item.FilePath);
                var gameId = GameIdReader.ReadFromStream(disc.IsoStream, disc.IsoLength);

                return (gameId, size);
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
        var sorted = FileItems.OrderBy(i => i.GameId, StringComparer.OrdinalIgnoreCase).ToList();

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
        var primary = FileItems.FirstOrDefault(i => i.No == 1);

        if (primary == null || primary.GameId == _lastIconGameId || primary.GameId is "인식중..." or "인식실패")
            return;

        _lastIconGameId = primary.GameId;
        _iconCts?.Cancel();
        _iconCts = new CancellationTokenSource();
        var ct = _iconCts.Token;

        var icon0Png = await CoverArtFetcher.TryDownloadIconPngAsync(primary.GameId, ct);

        if (ct.IsCancellationRequested) 
            return;

        _icon0Bytes = icon0Png ?? PbpResources.ICON0;
        Icon0Image = _icon0Bytes.ToBitmapImage();

        var meta = GameMetadataLookup.Find(primary.GameId);

        var pic0Png = meta != null ? await GameMetadataLookup.TryDownloadImagePngAsync(meta.Pic0, ct) : null;

        if (ct.IsCancellationRequested) 
            return;

        _pic0Bytes = pic0Png ?? PbpResources.PIC0;
        Pic0Image = _pic0Bytes.ToBitmapImage();

        var pic1Png = meta != null ? await GameMetadataLookup.TryDownloadImagePngAsync(meta.Pic1, ct) : null;

        if (ct.IsCancellationRequested) 
            return;

        _pic1Bytes = pic1Png ?? PbpResources.PIC1;
        Pic1Image = _pic1Bytes.ToBitmapImage();

        if (meta != null && !string.IsNullOrWhiteSpace(meta.Title))
            GameTitle = meta.Title;
    }

    private async Task RunAsync()
    {
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        IsConverting = true;

        try
        {
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

            var orderedItems = FileItems.OrderBy(i => i.No).ToList();
            var gameTitle = string.IsNullOrWhiteSpace(GameTitle) ? GuessTitle(orderedItems[0]) : GameTitle;
            var mainGameId = orderedItems[0].GameId;

            var assets = new PbpAssets
            {
                Icon0Png = _icon0Bytes,
                Pic0Png = _pic0Bytes,
                Pic1Png = _pic1Bytes
            };

            try
            {
                AppendLog($"작업 시작", LogLevel.Highlight);

                if (orderedItems.Count == 1)
                {
                    var item = orderedItems[0];
                    var progress = new Progress<ProgressInfo>(p => item.Progress = p.Percent);

                    await PbpPackager.WriteSingleDiscAsync(item.FilePath, mainGameId, gameTitle, 9, assets,
                        progress, (msg, lvl, id) => AppendLog(msg, lvl), _cts.Token);
                }
                else
                {
                    var discs = orderedItems
                        .Select(i => (InputPath: i.FilePath, GameTitle: $"{gameTitle} - Disc {i.No}"))
                        .ToList();

                    var outputPath = Path.Combine(Path.GetDirectoryName(orderedItems[0].FilePath)!, $"{gameTitle}.pbp");
                    var progress = new Progress<ProgressInfo>(p =>
                    {
                        foreach (var i in orderedItems) i.Progress = p.Percent;
                    });

                    await PbpPackager.WriteMultiDiscAsync(discs, mainGameId, gameTitle, outputPath, 9, assets,
                        progress, (msg, lvl, id) => AppendLog(msg, lvl), _cts.Token);
                }

                foreach (var i in orderedItems) i.Progress = 100;
                AppendLog($"완료: {gameTitle} ({orderedItems.Count}개 디스크)", LogLevel.Ok);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppendLog($"오류: [{gameTitle}]{ex.Message}", LogLevel.Error);                
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("작업이 취소되었습니다.", LogLevel.Error);
        }
        finally
        {
            IsConverting = false;
        }
    }

    private static string GuessTitle(DiscFileItem item)
        => System.Text.RegularExpressions.Regex.Replace(
            Path.GetFileNameWithoutExtension(item.FilePath), @"\s*\(Disc\s*\d+\)", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

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