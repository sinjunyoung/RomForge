using Common.Localization;
using Microsoft.Win32;
using RomForge.Core.UI.Command;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RomForge.Core.ViewModels.Settings;

public class KeyFilePathItemViewModel : ViewModelBase
{
    private readonly Func<string?> _getPath;
    private readonly Action<string?> _setPath;
    private readonly Func<bool>? _validate;
    private readonly string _fileFilter;

    public string Label { get; }

    public string DefaultFileName { get; }

    public string Hint => Loc.S.HintKeyFileEmptyUsesExeFolder(DefaultFileName);

    public string? FilePath
    {
        get => _getPath();
        set => Accept(value);
    }

    public string? ErrorMessage
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(ErrorVisibility));
        }
    }

    public Visibility ErrorVisibility => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility HintVisibility => string.IsNullOrWhiteSpace(FilePath) ? Visibility.Visible : Visibility.Collapsed;

    public bool IsFound => IsCustomFound || File.Exists(DefaultPath);

    public string StatusText
    {
        get
        {
            if (IsCustomFound)
                return Loc.S.LabelKeyFileUsingSpecified;

            bool defaultFound = File.Exists(DefaultPath);

            if (!string.IsNullOrWhiteSpace(FilePath))
                return defaultFound ? Loc.S.LabelKeyFileMissingUsingExe : Loc.S.LabelKeyFileSpecifiedNotFound;

            return defaultFound ? Loc.S.LabelKeyFileFoundInExe : Loc.S.LabelKeyFileNone;
        }
    }

    public Brush StatusBrush => IsFound ? Brushes.LightGreen : Brushes.IndianRed;

    public ICommand BrowseCommand { get; }

    public ICommand ClearCommand { get; }

    private string DefaultPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultFileName);

    private bool IsCustomFound => !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);

    public KeyFilePathItemViewModel(string label, string defaultFileName, string fileFilter, KeyFilesConfig config, Func<string?> getPath, Action<string?> setPath, Func<bool>? validate = null)
    {
        Label = label;
        DefaultFileName = defaultFileName;
        _fileFilter = fileFilter;
        _getPath = getPath;
        _setPath = setPath;
        _validate = validate;
        BrowseCommand = new RelayCommand(_ => Browse());
        ClearCommand = new RelayCommand(_ => Accept(null));
        config.PropertyChanged += (_, _) => Refresh();
    }

    public void Accept(string? path)
    {
        string? previousPath = _getPath();
        string? newPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();

        _setPath(newPath);

        if (newPath != null && _validate != null && !_validate())
        {
            _setPath(previousPath);
            ErrorMessage = Loc.S.ErrKeyFileInvalid;
        }
        else
        {
            ErrorMessage = null;
        }

        Refresh();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(HintVisibility));
        OnPropertyChanged(nameof(IsFound));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
    }

    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.S.DialogSelectKeyFile(DefaultFileName),
            Filter = _fileFilter
        };

        if (dialog.ShowDialog() == true)
            Accept(dialog.FileName);
    }
}