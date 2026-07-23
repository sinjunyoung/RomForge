using Common.WPF.ViewModels;
using System.Windows.Media;

namespace NSW.WPF.ViewModels;

public class GameFile(string filePath) : FileItemBase(filePath, Core.Properties.Resources.Status_Analyzing)
{
    private string? _titleName;
    private string? _titleId;
    private string? _version;
    private ImageSource? _icon;
    private string? _patchPath;

    public string FileType
    {
        get => Status;
        set => Status = value;
    }

    protected override void OnStatusChanged()
    {
        OnPropertyChanged(nameof(TypeBackground));
        OnPropertyChanged(nameof(TypeForeground));
    }

    public string? TitleName
    {
        get => _titleName;
        set => SetProperty(ref _titleName, value);
    }

    public string? TitleID
    {
        get => _titleId;
        set => SetProperty(ref _titleId, value);
    }

    public string? Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string? PatchPath
    {
        get => _patchPath;
        set
        {
            SetProperty(ref _patchPath, value);
            OnPropertyChanged(nameof(PatchDisplay));
            OnPropertyChanged(nameof(PatchIconSource));
        }
    }

    public string PatchDisplay => string.IsNullOrEmpty(PatchPath) ? "(없음)" : PatchPath;

    public string PatchIconSource => string.IsNullOrEmpty(PatchPath)
        ? "/Assets/Images/NoPatch.png"
        : "/Assets/Images/Patch.png";

    public bool IsKeyMissing => FileType == Core.Properties.Resources.Status_NoKey;

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

            if (count > 1) return new SolidColorBrush(Color.FromArgb(50, 120, 0, 212));
            if (hasBase) return new SolidColorBrush(Color.FromArgb(50, 0, 120, 212));
            if (hasUpdate) return new SolidColorBrush(Color.FromArgb(50, 16, 124, 16));
            if (hasDlc) return new SolidColorBrush(Color.FromArgb(50, 200, 130, 0));

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

            if (count > 1) return new SolidColorBrush(Color.FromRgb(180, 100, 255));
            if (hasBase) return new SolidColorBrush(Color.FromRgb(100, 180, 255));
            if (hasUpdate) return new SolidColorBrush(Color.FromRgb(100, 210, 100));
            if (hasDlc) return new SolidColorBrush(Color.FromRgb(255, 190, 80));

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

    protected override string FormatSize(long bytes) => Common.Utils.FormatFileSize(bytes);
}