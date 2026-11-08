using Microsoft.Win32;
using RomForge.Core.UI.Command;
using System.Windows;
using System.Windows.Input;

namespace RomForge.Core.ViewModels;

public class KeyOverlayViewModel : ViewModelBase
{
    private readonly Func<bool> _isAvailable;
    private readonly Func<string?> _getPath;
    private readonly Action<string?> _setPath;
    private readonly string _fileFilter;
    private bool _isSkipped;

    public string IconSource { get; }

    public string Title { get; }

    public string Message { get; }

    public bool CanSkip { get; }

    public string? ErrorMessage
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(ErrorVisibility));
        }
    }

    public Visibility OverlayVisibility => !_isSkipped && !_isAvailable() ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SkipVisibility => CanSkip ? Visibility.Visible : Visibility.Collapsed;

    public ICommand BrowseCommand { get; }

    public ICommand SkipCommand { get; }

    public KeyOverlayViewModel(string iconSource, string title, string message, string fileFilter, Func<bool> isAvailable, Func<string?> getPath, Action<string?> setPath, bool canSkip = false)
    {
        IconSource = iconSource;
        Title = title;
        Message = message;
        CanSkip = canSkip;
        _fileFilter = fileFilter;
        _isAvailable = isAvailable;
        _getPath = getPath;
        _setPath = setPath;
        BrowseCommand = new RelayCommand(_ => Browse());
        SkipCommand = new RelayCommand(_ =>
        {
            _isSkipped = true;
            Refresh();
        });
    }

    public void Refresh() => OnPropertyChanged(nameof(OverlayVisibility));

    public void Accept(string path)
    {
        string? previousPath = _getPath();

        _setPath(path);

        if (_isAvailable())
            ErrorMessage = null;
        else
        {
            _setPath(previousPath);
            ErrorMessage = "선택한 파일을 키 파일로 사용할 수 없습니다. 올바른 파일인지 확인하세요.";
        }

        Refresh();
    }

    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "키 파일 선택",
            Filter = _fileFilter
        };

        if (dialog.ShowDialog() == true)
            Accept(dialog.FileName);
    }
}