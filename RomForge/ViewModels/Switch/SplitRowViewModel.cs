using Common.WPF.ViewModels;
using System.IO;
using System.Windows.Media;
using Res = NSW.Properties.Resources;

namespace RomForge.ViewModels.Switch;

public class SplitRowViewModel(string filePath, SplitRowViewModel? parent = null) : ViewModelBase
{
    private bool _ownChecked = true;
    private bool _isExpanded;
    private string _name = Path.GetFileNameWithoutExtension(filePath);
    private string _typeText = string.Empty;
    private string _titleId = string.Empty;
    private string _version = string.Empty;
    private long _sizeBytes;
    private ImageSource? _icon;

    public string FilePath { get; } = filePath;

    public SplitRowViewModel? Parent { get; } = parent;

    public List<SplitRowViewModel> Children { get; } = [];

    public bool Analyzed { get; set; }

    public bool IsChild => Parent != null;

    public bool CanExpand => Parent == null && Children.Count > 1;

    public string ExpanderText => _isExpanded ? "−" : "+";

    public string Extension => IsChild ? string.Empty : Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
                OnPropertyChanged(nameof(ExpanderText));
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string TypeText
    {
        get => _typeText;
        set
        {
            if (SetProperty(ref _typeText, value))
            {
                OnPropertyChanged(nameof(TypeBackground));
                OnPropertyChanged(nameof(TypeForeground));
            }
        }
    }

    public string TitleId
    {
        get => _titleId;
        set => SetProperty(ref _titleId, value);
    }

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (SetProperty(ref _sizeBytes, value))
                OnPropertyChanged(nameof(SizeText));
        }
    }

    public string SizeText => Common.Utils.FormatFileSize(_sizeBytes);

    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public bool? IsChecked
    {
        get
        {
            if (Children.Count <= 1)
                return _ownChecked;

            int on = Children.Count(c => c._ownChecked);

            if (on == Children.Count)
                return true;

            if (on == 0)
                return false;

            return null;
        }
        set
        {
            bool v = value == true;

            if (Children.Count > 1)
            {
                foreach (var child in Children)
                    child.SetOwn(v);

                OnPropertyChanged();
                return;
            }

            SetOwn(v);
            Parent?.OnPropertyChanged(nameof(IsChecked));
        }
    }

    public void NotifyStructureChanged()
    {
        OnPropertyChanged(nameof(CanExpand));
        OnPropertyChanged(nameof(IsChecked));
    }

    public void SetAll(bool value)
    {
        if (Children.Count > 1)
        {
            foreach (var child in Children)
                child.SetOwn(value);

            OnPropertyChanged(nameof(IsChecked));
            return;
        }

        SetOwn(value);
    }

    public void SetExceptBase()
    {
        if (Children.Count > 1)
        {
            foreach (var child in Children)
                child.SetOwn(child.TypeText != "B");

            OnPropertyChanged(nameof(IsChecked));
            return;
        }

        SetOwn(TypeText != "B");
    }

    public IReadOnlySet<string> GetSelectedTitleIds()
    {
        if (Children.Count > 1)
            return new HashSet<string>(Children.Where(c => c._ownChecked).Select(c => c.TitleId), StringComparer.OrdinalIgnoreCase);

        if (Analyzed && _ownChecked && !string.IsNullOrEmpty(_titleId))
            return new HashSet<string>([_titleId], StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private void SetOwn(bool value)
    {
        if (_ownChecked == value)
            return;

        _ownChecked = value;
        OnPropertyChanged(nameof(IsChecked));
    }

    public Brush TypeBackground
    {
        get
        {
            if (_typeText == Res.Status_NoKey)
                return new SolidColorBrush(Color.FromArgb(60, 220, 50, 50));

            if (_typeText == "?")
                return new SolidColorBrush(Color.FromArgb(60, 200, 160, 0));

            bool hasBase = _typeText.Contains('B');
            bool hasUpdate = _typeText.Contains('U');
            bool hasDlc = _typeText.Contains('D');
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
            if (_typeText == Res.Status_NoKey)
                return new SolidColorBrush(Color.FromRgb(255, 100, 100));

            if (_typeText == "?")
                return new SolidColorBrush(Color.FromRgb(255, 210, 80));

            bool hasBase = _typeText.Contains('B');
            bool hasUpdate = _typeText.Contains('U');
            bool hasDlc = _typeText.Contains('D');
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

    public Brush ExtensionBackground => Extension switch
    {
        "nsp" => new SolidColorBrush(Color.FromArgb(60, 0, 150, 255)),
        "nsz" => new SolidColorBrush(Color.FromArgb(120, 0, 100, 200)),
        "xci" => new SolidColorBrush(Color.FromArgb(60, 0, 180, 80)),
        "xcz" => new SolidColorBrush(Color.FromArgb(120, 0, 130, 40)),
        _ => new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
    };

    public Brush ExtensionForeground => Extension switch
    {
        "nsp" => new SolidColorBrush(Color.FromRgb(100, 200, 255)),
        "nsz" => new SolidColorBrush(Color.FromRgb(200, 230, 255)),
        "xci" => new SolidColorBrush(Color.FromRgb(100, 255, 150)),
        "xcz" => new SolidColorBrush(Color.FromRgb(200, 255, 200)),
        _ => new SolidColorBrush(Color.FromRgb(160, 160, 160))
    };
}