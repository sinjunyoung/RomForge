using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using Vita.Core.Models;
using Vita.Core.Services;

namespace RomForge.Core.ViewModels.PSVita;

public class SourceRowViewModel : ViewModelBase
{
    private CancellationTokenSource? _iconReloadCts;

    public SourceKind Kind { get; }

    public string? ItemSourcePath { get; }

    public string? FileName => Kind switch
    {
        SourceKind.Pkg => System.IO.Path.GetFileName(Path),
        SourceKind.Unpacked => TitleId,
        _ => $"{System.IO.Path.GetFileName(Path?.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))} :: {ItemSourcePath}"
    };

    public bool IsPkg => Kind == SourceKind.Pkg;

    public bool IsUnpacked => Kind == SourceKind.Unpacked;

    public string? TitleId { get; }

    public string? ContentIdSuffix { get; set; }

    public ContentCategory? Category { get; set { SetProperty(ref field, value); }
    }

    public string? License
    {
        get;
        set
        {
            SetProperty(ref field, value);
            CommandManager.InvalidateRequerySuggested();

            if (Kind == SourceKind.Pkg && Category == ContentCategory.App)
                _ = DebounceReloadIconAsync();
        }
    } = string.Empty;

    public string Path { get; set { SetProperty(ref field, value); CommandManager.InvalidateRequerySuggested(); } } = string.Empty;

    public string? PatchPath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(PatchIconSource)); CommandManager.InvalidateRequerySuggested(); } } = string.Empty;

    public string? ErrorMessage { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(IsValid)); } } = string.Empty;

    public bool IsValid => ErrorMessage is null;

    public bool IsLicenseEditable { get; set { SetProperty(ref field, value); } } = true;

    public long SizeBytes { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(Size)); } } = -1;

    public string Size => SizeBytes < 0 ? "계산 중..." : FormatBytes(SizeBytes);

    public byte[]? IconBytes { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(IconImageSource)); } }

    public ImageSource? IconImageSource => IconBytes?.ToBitmapImage();

    public string? GameName { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(DisplayTitle)); } }

    public string? DisplayTitle => string.IsNullOrWhiteSpace(GameName) ? FileName : GameName;

    public string PatchIconSource => string.IsNullOrEmpty(PatchPath) ? "/Assets/Images/NoPatch.png" : "/Assets/Images/Patch.png";

    public SolidColorBrush CategoryBadgeColor => Category switch
    {
        ContentCategory.App => new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
        ContentCategory.Patch => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
        ContentCategory.Addcont => new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)),
        _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
    };

    private SourceRowViewModel(string path, SourceKind kind, string? itemSourcePath, string? titleId, ContentCategory? category, string? contentIdSuffix)
    {
        Path = path;
        Kind = kind;
        ItemSourcePath = itemSourcePath;
        TitleId = titleId;
        Category = category;
        ContentIdSuffix = contentIdSuffix;
    }

    public static SourceRowViewModel FromPkg(string pkgPath)
    {
        var result = PkgProbe.Probe(pkgPath);
        return new SourceRowViewModel(pkgPath, SourceKind.Pkg, null, result.TitleId, result.Category, result.ContentIdSuffix);
    }

    public static SourceRowViewModel FromUnpacked(UnpackedTitle title) => new(title.FolderPath, SourceKind.Unpacked, null, title.TitleId, ContentCategory.App, null)
    {
        GameName = title.TitleName,
        Category = title.Category,
        ContentIdSuffix = title.ContentIdSuffix,
        IconBytes = title.IconBytes,
        SizeBytes = title.SizeBytes,
        PatchPath = title.PatchPath ?? string.Empty,
        IsLicenseEditable = false
    };

    public static SourceRowViewModel FromZipItem(string containerPath, SourceItem item) => new(containerPath, SourceKind.ZipOrFolder, item.SourcePath, item.TitleId, item.Category, item.ContentIdSuffix);

    public static List<SourceRowViewModel> DiscoverFromContainer(string containerPath)
    {
        using var accessor = SourceAccessorFactory.Open(containerPath);
        var items = SourcePreparer.DiscoverItems(accessor);

        if (items.Count == 0)
            throw new InvalidDataException("app/patch/addcont 폴더를 찾을 수 없습니다.");

        return [.. items.Select(item => FromZipItem(containerPath, item))];
    }

    public async Task LoadMetadataAsync()
    {
        await RefreshSizeAsync();
        await LoadIconAndTitleAsync();
    }

    private async Task RefreshSizeAsync()
    {
        try
        {
            long bytes = await Task.Run(() =>
            {
                if (Kind == SourceKind.Pkg)
                    return new FileInfo(Path).Length;

                using var accessor = SourceAccessorFactory.Open(Path);
                string prefix = string.IsNullOrEmpty(ItemSourcePath) ? string.Empty : ItemSourcePath.Replace('\\', '/').Trim('/') + "/";
                long total = 0;

                foreach (var file in accessor.EnumerateAllFiles())
                {
                    string normalized = file.Replace('\\', '/');

                    if (prefix.Length == 0 || normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        total += accessor.GetFileSize(file);
                }

                return total;
            });

            SizeBytes = bytes;
        }
        catch
        {
            SizeBytes = 0;
        }
    }

    private async Task LoadIconAndTitleAsync()
    {
        try
        {
            var result = await Task.Run(() =>
            {
                if (Kind == SourceKind.Pkg)
                {
                    using var accessor = new PkgSourceAccessor(Path);
                    string? pkgTitle = accessor.FileExists("sce_sys/param.sfo") ? SfoParser.GetString(SfoParser.Parse(accessor.ReadAllBytes("sce_sys/param.sfo")), "TITLE") : null;
                    byte[]? pkgIcon = null;

                    if (Category == ContentCategory.App && !string.IsNullOrWhiteSpace(License))
                    {
                        try
                        {
                            byte[] klicensee = PkgLicenseResolver.ResolveKlicensee(License, accessor.Header.ContentId);
                            var pkgTable = NoNpDrmDecryptor.ParseFileTable(accessor, string.Empty);

                            for (int i = 0; i < pkgTable.Entries.Count; i++)
                            {
                                var entry = pkgTable.Entries[i];

                                if (entry.Type.IsDirectory())
                                    continue;

                                string relativePath = (entry.RelativePath ?? entry.Name).Replace('\\', '/').TrimStart('/');

                                if (!relativePath.Equals("sce_sys/icon0.png", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                pkgIcon = NoNpDrmDecryptor.DecryptEntry(accessor, string.Empty, klicensee, entry, pkgTable.UnicvEntries[i], pkgTable.FilesSalt);
                                break;
                            }
                        }
                        catch { }
                    }

                    return (pkgIcon, pkgTitle);
                }

                using var containerAccessor = SourceAccessorFactory.Open(Path);
                string sfoRel = $"{ItemSourcePath}/sce_sys/param.sfo";
                string? title = containerAccessor.FileExists(sfoRel) ? SfoParser.GetString(SfoParser.Parse(containerAccessor.ReadAllBytes(sfoRel)), "TITLE") : null;
                byte[]? icon = null;

                if (Category == ContentCategory.App)
                {
                    string workBinRel = $"{ItemSourcePath}/sce_sys/package/work.bin";

                    if (containerAccessor.FileExists(workBinRel))
                    {
                        var license = WorkBinReader.Read(containerAccessor, workBinRel);
                        var table = NoNpDrmDecryptor.ParseFileTable(containerAccessor, ItemSourcePath!);

                        for (int i = 0; i < table.Entries.Count; i++)
                        {
                            var entry = table.Entries[i];

                            if (entry.Type.IsDirectory())
                                continue;

                            string relativePath = (entry.RelativePath ?? entry.Name).Replace('\\', '/').TrimStart('/');

                            if (!relativePath.Equals("sce_sys/icon0.png", StringComparison.OrdinalIgnoreCase))
                                continue;

                            try
                            {
                                icon = NoNpDrmDecryptor.DecryptEntry(containerAccessor, ItemSourcePath!, license.Klicensee, entry, table.UnicvEntries[i], table.FilesSalt);
                            }
                            catch { }

                            break;
                        }
                    }
                }

                return (icon, title);
            });

            IconBytes = result.Item1;
            GameName = result.Item2;
        }
        catch
        {
            IconBytes = null;
            GameName = null;
        }
    }

    private async Task DebounceReloadIconAsync()
    {
        _iconReloadCts?.Cancel();

        var cts = new CancellationTokenSource();

        _iconReloadCts = cts;

        try
        {
            await Task.Delay(500, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested)
            return;

        await LoadIconAndTitleAsync();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    public UnpackedTitle ToUnpackedTitle() => new()
    {
        TitleId = TitleId,
        FolderPath = Path,
        TitleName = GameName,
        IconBytes = IconBytes,
        SizeBytes = SizeBytes,
        PatchPath = string.IsNullOrWhiteSpace(PatchPath) ? null : PatchPath
    };

    public BatchSourceEntry ToBatchEntry() => Kind == SourceKind.Pkg
        ? new BatchSourceEntry
        {
            Kind = SourceKind.Pkg,
            Path = Path,
            License = License,
            PatchPath = string.IsNullOrWhiteSpace(PatchPath) ? null : PatchPath,
            Probe = new PkgProbeResult { TitleId = TitleId, Category = Category, ContentIdSuffix = ContentIdSuffix, ContentId = string.Empty }
        }
        : new BatchSourceEntry
        {
            Kind = SourceKind.ZipOrFolder,
            Path = Path,
            PatchPath = string.IsNullOrWhiteSpace(PatchPath) ? null : PatchPath,
            ItemCategory = Category,
            ItemTitleId = TitleId,
            ItemContentIdSuffix = ContentIdSuffix,
            ItemSourcePath = ItemSourcePath
        };
}