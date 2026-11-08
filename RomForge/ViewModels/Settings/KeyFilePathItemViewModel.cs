using Microsoft.Win32;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RomForge.ViewModels.Settings;

public class KeyFilePathItemViewModel : ViewModelBase
{
    private readonly Func<string?> _getPath;
    private readonly Action<string?> _setPath;
    private readonly string _fileFilter;

    public string Label { get; }

    public string DefaultFileName { get; }

    public bool IsOptional { get; }

    public string Hint => $"비워두면 exe 폴더의 {DefaultFileName}을(를) 사용합니다";

    public string? FilePath
    {
        get => _getPath();
        set
        {
            _setPath(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
            Refresh();
        }
    }

    public Visibility HintVisibility => string.IsNullOrWhiteSpace(FilePath) ? Visibility.Visible : Visibility.Collapsed;

    public bool IsFound => IsCustomFound || File.Exists(DefaultPath);

    public string StatusText
    {
        get
        {
            if (IsCustomFound)
                return "지정한 파일 사용 중";

            bool defaultFound = File.Exists(DefaultPath);

            if (!string.IsNullOrWhiteSpace(FilePath))
                return defaultFound ? "지정한 파일이 없어 exe 폴더의 파일 사용 중" : "지정한 파일을 찾을 수 없음";

            if (defaultFound)
                return "exe 폴더에서 찾음";

            return IsOptional ? "파일 없음 (선택 사항)" : "파일 없음";
        }
    }

    public Brush StatusBrush => IsFound ? Brushes.LightGreen : IsOptional ? Brushes.Gray : Brushes.IndianRed;

    public ICommand BrowseCommand { get; }

    public ICommand ClearCommand { get; }

    private string DefaultPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultFileName);

    private bool IsCustomFound => !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);

    public KeyFilePathItemViewModel(string label, string defaultFileName, string fileFilter, bool isOptional, Func<string?> getPath, Action<string?> setPath)
    {
        Label = label;
        DefaultFileName = defaultFileName;
        IsOptional = isOptional;
        _fileFilter = fileFilter;
        _getPath = getPath;
        _setPath = setPath;
        BrowseCommand = new RelayCommand(_ => Browse());
        ClearCommand = new RelayCommand(_ => FilePath = null);
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
            Title = $"{DefaultFileName} 선택",
            Filter = _fileFilter
        };

        if (dialog.ShowDialog() == true)
            FilePath = dialog.FileName;
    }
}