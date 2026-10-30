using System.IO;
using System.Windows.Media;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.ViewModels.Switch;

public class SplitRowViewModel(string filePath, SplitRowViewModel? parent = null) : ViewModelBase
{
    private bool _ownChecked = true;

    public string FilePath { get; } = filePath;

    public SplitRowViewModel? Parent { get; } = parent;

    public List<SplitRowViewModel> Children { get; } = [];

    public bool Analyzed { get; set; }

    public bool IsChild => Parent != null;

    public bool CanExpand => Parent == null && Children.Count > 1;

    public string ExpanderText => IsExpanded ? "−" : "+";

    public string Extension => IsChild ? string.Empty : Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();

    public bool IsExpanded
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(ExpanderText));
        }
    }

    public string Name { get; set => SetProperty(ref field, value); } = Path.GetFileNameWithoutExtension(filePath);

    public string TypeText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(TypeBackground));
                OnPropertyChanged(nameof(TypeForeground));
            }
        }
    } = string.Empty;

    public string TitleId { get; set => SetProperty(ref field, value); } = string.Empty;

    public string Version { get; set => SetProperty(ref field, value); } = string.Empty;

    public long SizeBytes
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(SizeText));
        }
    }

    public string SizeText => Common.Utils.FormatFileSize(SizeBytes);

    public ImageSource? Icon { get; set => SetProperty(ref field, value); }

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

        if (Analyzed && _ownChecked && !string.IsNullOrEmpty(TitleId))
            return new HashSet<string>([TitleId], StringComparer.OrdinalIgnoreCase);

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
            if (TypeText == Res.Status_NoKey)
                return new SolidColorBrush(Color.FromArgb(60, 220, 50, 50));

            if (TypeText == "?")
                return new SolidColorBrush(Color.FromArgb(60, 200, 160, 0));

            bool hasBase = TypeText.Contains('B');
            bool hasUpdate = TypeText.Contains('U');
            bool hasDlc = TypeText.Contains('D');
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
            if (TypeText == Res.Status_NoKey)
                return new SolidColorBrush(Color.FromRgb(255, 100, 100));

            if (TypeText == "?")
                return new SolidColorBrush(Color.FromRgb(255, 210, 80));

            bool hasBase = TypeText.Contains('B');
            bool hasUpdate = TypeText.Contains('U');
            bool hasDlc = TypeText.Contains('D');
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