using Common;
using Common.WPF.ViewModels;
using Microsoft.VisualBasic.FileIO;
using NSW.WPF.Services;
using PBP.Core.Models;
using PBP.Core.Services;
using RomForge.Core;
using RomForge.Core.Services.PS;
using RomForge.Helpers;
using RomForge.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels.PS;

public class PackingMainViewModel : ToolTabViewModel
{
    private const int MaxItems = 5;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cue", ".m3u", ".iso", ".chd"
    };

    public ObservableCollection<string> FmvFixPresets { get; } = [
        "0x04", "0x07", "0xFB" ];

    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _iconCts;

    private readonly AppConfig _config;

    private string? _lastIconGameId;
    private string _gameTitle = string.Empty;
    private int _progressPct;
    private string _progressLabel = string.Empty;
    private string _progressPercent = "0%";
    private string _progressTime = string.Empty;
    private string _progressSpeed = string.Empty;
    private bool _isDownloading;
    private bool _isValidating;
    private bool _useFmvFix;
    private string _fmvFixValue = "0x04";
    private bool _useCdTimingFix;

    public byte[] Icon0Bytes { get; set; } = EmbeddedAssetProvider.GetDefaultIcon0();

    public byte[] Pic0Bytes { get; set; } = EmbeddedAssetProvider.GetDefaultPic0();

    public byte[] Pic1Bytes { get; set; } = EmbeddedAssetProvider.GetDefaultPic1();

    public byte[] BootLogoBytes { get; set; }

    public string GameTitle
    {
        get => _gameTitle;
        set { _gameTitle = value; OnPropertyChanged(); }
    }

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

    public bool IsDownloading
    {
        get => _isDownloading;
        set 
        { 
            _isDownloading = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(CanRun));
            Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    public bool IsValidating
    {
        get => _isValidating;
        set 
        {
            _isValidating = value; 
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanAdd));
        }
    }

    public bool UseFmvFix
    {
        get => _useFmvFix;
        set => SetProperty(ref _useFmvFix, value);
    }

    public string FmvFixValue
    {
        get => _fmvFixValue;
        set => SetProperty(ref _fmvFixValue, value);
    }

    public bool UseCdTimingFix
    {
        get => _useCdTimingFix;
        set => SetProperty(ref _useCdTimingFix, value);
    }


    private BitmapImage? _icon0Image;
    public BitmapImage? Icon0Image
    {
        get => _icon0Image;
        set => SetProperty(ref _icon0Image, value);
    }

    private BitmapImage? _pic0Image;
    public BitmapImage? Pic0Image
    {
        get => _pic0Image;
        set => SetProperty(ref _pic0Image, value);
    }

    private BitmapImage? _pic1Image;
    public BitmapImage? Pic1Image
    {
        get => _pic1Image;
        set => SetProperty(ref _pic1Image, value);
    }

    private BitmapImage? _bootLogoImage;
    public BitmapImage? BootLogoImage
    {
        get => _bootLogoImage;
        set
        {
            SetProperty(ref _bootLogoImage, value);
            OnPropertyChanged(nameof(HasBootLogo));
            OnPropertyChanged(nameof(ShowBootLogoHint));
        }
    }

    public bool HasBootLogo => BootLogoImage != null;

    public bool ShowBootLogoHint => !HasBootLogo;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ObservableCollection<DiscFileItem> FileItems { get; } = [];

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool CanAdd => !IsLocked && FileItems.Count < MaxItems;

    public bool CanRun => !IsLocked && FileItems.Count > 0 && !IsDownloading;


    public ICommand RunCommand { get; }

    public ICommand SettingsCommand { get; }
    

    public event EventHandler RunNavigateSettings;

    public PackingMainViewModel(AppConfig config)
    {
        _config = config;
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => CanRun);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsLocked);
        SettingsCommand = new RelayCommand(async _ => RunNavigateSettings?.Invoke(this, EventArgs.Empty), _ => !IsLocked);

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

    public async void AddPaths(IEnumerable<string> paths)
    {
        try
        {
            await AddPathsCoreAsync(paths);
        }
        catch (Exception ex)
        {
            AppendLog($"파일 추가 중 오류: {ex.Message}", LogLevel.Error);
            IsValidating = false;
        }
    }

    private async Task AddPathsCoreAsync(IEnumerable<string> paths)
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

        if (newCandidates.Count == 0)
            return;

        var room = MaxItems - FileItems.Count;
        var toValidate = newCandidates.Take(Math.Max(room, 0)).ToList();
        int userRejectedCount = newCandidates.Count - toValidate.Count;

        if (toValidate.Count == 0)
        {
            if (userRejectedCount > 0)
                AppendLog($"최대 {MaxItems}개까지만 등록할 수 있습니다. {userRejectedCount}개 파일이 제외되었습니다.", LogLevel.Error);

            return;
        }

        IsValidating = true;

        List<(string Path, string GameId, long Size)> validated;
        int failedCount;

        try
        {
            var results = await Task.WhenAll(toValidate.Select(ValidateCandidateAsync));

            validated = [.. results.Where(r => r != null).Select(r => r!.Value)];
            failedCount = results.Length - validated.Count;
        }
        finally
        {
            IsValidating = false;
        }

        foreach (var v in validated)
        {
            var item = new DiscFileItem(v.Path)
            {
                GameId = v.GameId,
                FileSizeBytes = v.Size
            };

            FileItems.Add(item);
        }

        if (userRejectedCount > 0)
            AppendLog($"최대 {MaxItems}개까지만 등록할 수 있습니다. {userRejectedCount}개 파일이 제외되었습니다.", LogLevel.Error);

        if (failedCount > 0)
            AppendLog($"{failedCount}개 파일에서 GameID 인식에 실패해 제외되었습니다.", LogLevel.Error);

        if (validated.Count > 0)
        {
            OnPropertyChanged(nameof(HintVisibility));
            ResortAndRenumber();
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private async Task<(string Path, string GameId, long Size)?> ValidateCandidateAsync(string path)
    {
        try
        {
            return await Task.Run(() =>
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                var size = DiscSizeResolver.GetTotalSize(path);

                if (ext == ".chd")
                    return (path, GameIdReader.ReadFromDisk(DiskSource.FromChd(path)), size);

                if (ext == ".cue")
                {
                    var binPath = CueFileResolver.GetBinPath(path);
                    using var stream = new FileStream(binPath, FileMode.Open, FileAccess.Read);

                    return (path, GameIdReader.ReadFromStream(stream, stream.Length), size);
                }

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);

                return (path, GameIdReader.ReadFromStream(fs, fs.Length), size);
            });
        }
        catch (Exception ex)
        {
            AppendLog($"[{Path.GetFileName(path)}] GameID 인식 실패: {ex.Message}", LogLevel.Error);

            return null;
        }
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
        Icon0Image = EmbeddedAssetProvider.GetDefaultIcon0().ToBitmapImage();
        Pic0Image = EmbeddedAssetProvider.GetDefaultPic0().ToBitmapImage();
        Pic1Image = EmbeddedAssetProvider.GetDefaultPic1().ToBitmapImage();
        ResetBootLogo();
    }

    public void SetIcon0FromBytes(byte[] rawBytes) => SetImage(rawBytes, (bytes, img) => { Icon0Bytes = bytes; Icon0Image = img; }, 80, 80);
    public void SetPic0FromBytes(byte[] rawBytes) => SetImage(rawBytes, (bytes, img) => { Pic0Bytes = bytes; Pic0Image = img; }, 270, 150);
    public void SetPic1FromBytes(byte[] rawBytes) => SetImage(rawBytes, (bytes, img) => { Pic1Bytes = bytes; Pic1Image = img; }, 480, 272);
    public void SetBootLogoFromBytes(byte[] rawBytes) => SetImage(rawBytes, (bytes, img) => { BootLogoBytes = bytes; BootLogoImage = img; }, 480, 272);

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

            IsDownloading = true;

            var meta = GameMetadataLookup.Find(primary.GameId);

            if (meta != null && !string.IsNullOrWhiteSpace(meta.ETitle))
                GameTitle = meta.ETitle;

            _lastIconGameId = primary.GameId;

            var old = Interlocked.Exchange(ref _iconCts, new CancellationTokenSource());

            if (old != null)
            {
                old.Cancel();
                old.Dispose();
            }

            var ct = _iconCts.Token;
            var icon0Png = await CoverArtFetcher.TryDownloadIconPngAsync(primary.GameId, ct);

            ct.ThrowIfCancellationRequested();

            Icon0Bytes = icon0Png ?? EmbeddedAssetProvider.GetDefaultIcon0();
            Icon0Image = Icon0Bytes.ToBitmapImage();

            var pic0Png = meta != null ? await GameMetadataLookup.TryDownloadImagePngAsync(meta.Pic0, ct) : null;

            ct.ThrowIfCancellationRequested();

            Pic0Bytes = pic0Png ?? EmbeddedAssetProvider.GetDefaultPic0();
            Pic0Image = Pic0Bytes.ToBitmapImage();

            var pic1Png = meta != null ? await GameMetadataLookup.TryDownloadImagePngAsync(meta.Pic1, ct) : null;

            ct.ThrowIfCancellationRequested();

            Pic1Bytes = pic1Png ?? EmbeddedAssetProvider.GetDefaultPic1();
            Pic1Image = Pic1Bytes.ToBitmapImage();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            IsDownloading = false;
            throw;
        }

        IsDownloading = false;
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
                BootPng = BootLogoBytes?.ResizePng(480, 272),
                DataPsp = EmbeddedAssetProvider.GetDefaultData()
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

            targetOutputPath = Utils.GetUniqueFilePath(targetOutputPath);

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
                    discInfos.Add(new DiscWriteInfo(resolved.IsoStream, resolved.IsoLength, item.GameId, orderedItems.Count > 1 ? $"{gameTitle} - Disc {item.No}" : gameTitle, resolved.TocData));
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

    public void ResetBootLogo()
    {
        BootLogoImage = null;        
    }
}