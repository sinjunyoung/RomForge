using NSW.Core.Models;
using System.Windows.Media;

namespace RomForge.Core.ViewModels.Switch;

public class GameFile(string filePath) : FileItemBase(filePath, "대기중")
{
    private long? _overrideSizeBytes;
    private bool _isLz4;
    private bool _lz4UserChanged;

    public string FileType
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsKeyMissing));
                OnPropertyChanged(nameof(TypeBackground));
                OnPropertyChanged(nameof(TypeForeground));
                OnPropertyChanged(nameof(CanSetLz4));
            }
        }
    } = string.Empty;

    public bool SourceHasCompressedRomfs { get; set; }

    public bool SourceLz4 { get; private set; }

    public bool? UnpackedLz4 { get; private set; }

    public bool CanSetLz4 => FileType.Contains('B') || FileType.Contains('U') || FileType.Contains('D');

    public bool IsLz4
    {
        get => _isLz4;
        set
        {
            if (SetProperty(ref _isLz4, value))
                _lz4UserChanged = true;
        }
    }

    public void ApplyLz4Detection(bool sourceLz4, bool? unpackedLz4)
    {
        SourceLz4 = sourceLz4;
        UnpackedLz4 = unpackedLz4;

        if (_lz4UserChanged)
            return;

        SetProperty(ref _isLz4, unpackedLz4 ?? sourceLz4, nameof(IsLz4));
    }

    public bool? GetLz4Override(bool rebuildOnly)
    {
        if (!CanSetLz4)
            return null;

        bool? baseline = rebuildOnly ? UnpackedLz4 : SourceLz4;

        return baseline.HasValue && (_lz4UserChanged || IsLz4 != baseline.Value) ? IsLz4 : null;
    }

    public long CustomSizeBytes
    {
        get => _overrideSizeBytes ?? base.FileSizeBytes;
        set
        {
            if (_overrideSizeBytes != value)
            {
                _overrideSizeBytes = value;
                OnPropertyChanged(nameof(FileSizeBytes));
                OnPropertyChanged(nameof(FileSize));
            }
        }
    }

    public override long FileSizeBytes => CustomSizeBytes;

    public IReadOnlyList<byte> ProgramIdOffsets { get; set; } = [];

    public GameNamingInfo? Naming { get; set; }

    public string? TitleName { get; set => SetProperty(ref field, value); }

    public string? TitleID { get; set => SetProperty(ref field, value); }

    public string? Version { get; set => SetProperty(ref field, value); }

    public ImageSource? Icon { get; set => SetProperty(ref field, value); }

    public string? PatchPath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(PatchDisplay)); OnPropertyChanged(nameof(PatchIconSource)); } }

    public string? PatchPassword { get; set => SetProperty(ref field, value); }

    public string PatchDisplay => string.IsNullOrEmpty(PatchPath) ? "(없음)" : PatchPath;

    public string PatchIconSource => string.IsNullOrEmpty(PatchPath) ? "/Assets/Images/NoPatch.png" : "/Assets/Images/Patch.png";

    public bool IsKeyMissing => FileType == "키 없음";

    public Brush TypeBackground
    {
        get
        {
            if (IsKeyMissing)
                return new SolidColorBrush(Color.FromArgb(60, 220, 50, 50));

            if (FileType == "?")
                return new SolidColorBrush(Color.FromArgb(60, 200, 160, 0));

            bool hasBase = FileType.Contains('B');
            bool hasUpdate = FileType.Contains('U');
            bool hasDlc = FileType.Contains('D');
            int count = (hasBase ? 1 : 0) + (hasUpdate ? 1 : 0) + (hasDlc ? 1 : 0);

            if (count > 1)
                return new SolidColorBrush(Color.FromArgb(50, 120, 0, 212));

            if (hasBase)
                return new SolidColorBrush(Color.FromArgb(50, 0, 120, 212));

            if (hasUpdate)
                return new SolidColorBrush(Color.FromArgb(50, 16, 124, 16));

            if (hasDlc)
                return new SolidColorBrush(Color.FromArgb(50, 200, 130, 0));

            return new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
        }
    }

    public Brush TypeForeground
    {
        get
        {
            if (IsKeyMissing)
                return new SolidColorBrush(Color.FromRgb(255, 100, 100));

            if (FileType == "?")
                return new SolidColorBrush(Color.FromRgb(255, 210, 80));

            bool hasBase = FileType.Contains('B');
            bool hasUpdate = FileType.Contains('U');
            bool hasDlc = FileType.Contains('D');
            int count = (hasBase ? 1 : 0) + (hasUpdate ? 1 : 0) + (hasDlc ? 1 : 0);

            if (count > 1)
                return new SolidColorBrush(Color.FromRgb(180, 100, 255));

            if (hasBase)
                return new SolidColorBrush(Color.FromRgb(100, 180, 255));

            if (hasUpdate)
                return new SolidColorBrush(Color.FromRgb(100, 210, 100));

            if (hasDlc)
                return new SolidColorBrush(Color.FromRgb(255, 190, 80));

            return new SolidColorBrush(Color.FromRgb(160, 160, 160));
        }
    }

    public Brush ExtensionBackground
    {
        get
        {
            return Extension switch
            {
                "nsp" => new SolidColorBrush(Color.FromArgb(60, 0, 150, 255)),
                "nsz" => new SolidColorBrush(Color.FromArgb(120, 0, 100, 200)),
                "xci" => new SolidColorBrush(Color.FromArgb(60, 0, 180, 80)),
                "xcz" => new SolidColorBrush(Color.FromArgb(120, 0, 130, 40)),
                _ => new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
            };
        }
    }

    public Brush ExtensionForeground
    {
        get
        {
            return Extension switch
            {
                "nsp" => new SolidColorBrush(Color.FromRgb(100, 200, 255)),
                "nsz" => new SolidColorBrush(Color.FromRgb(200, 230, 255)),
                "xci" => new SolidColorBrush(Color.FromRgb(100, 255, 150)),
                "xcz" => new SolidColorBrush(Color.FromRgb(200, 255, 200)),
                _ => new SolidColorBrush(Color.FromRgb(160, 160, 160))
            };
        }
    }

    public static GameFile FromPath(string path) => new(path);

    protected override void OnStatusChanged() => base.OnStatusChanged();

    protected override string FormatSize(long bytes) => Common.Utils.FormatFileSize(bytes);
}