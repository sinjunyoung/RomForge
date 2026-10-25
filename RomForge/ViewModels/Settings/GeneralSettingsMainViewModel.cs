using Microsoft.Win32;
using RomForge.Core;
using RomForge.Core.Services;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels;
using System.Windows.Input;

namespace RomForge.ViewModels.Settings;

public class GeneralSettingsMainViewModel : ToolTabViewModel
{
    public GeneralSettingsMainViewModel()
    {
        AppConfig.Instance.Common.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CommonConfig.Topmost))
                OnPropertyChanged(nameof(Topmost));
            else if (e.PropertyName == nameof(CommonConfig.AutoCheckUpdate))
                OnPropertyChanged(nameof(AutoCheckUpdate));
            else if (e.PropertyName == nameof(CommonConfig.IsMuted))
                OnPropertyChanged(nameof(IsMuted));
            else if (e.PropertyName == nameof(CommonConfig.CompleteSoundPath))
            {
                OnPropertyChanged(nameof(CompleteSoundPath));
                OnPropertyChanged(nameof(CompleteSoundHintVisibility));
            }
            else if (e.PropertyName == nameof(CommonConfig.CancelSoundPath))
            {
                OnPropertyChanged(nameof(CancelSoundPath));
                OnPropertyChanged(nameof(CancelSoundHintVisibility));
            }
            else if (e.PropertyName == nameof(CommonConfig.ErrorSoundPath))
            {
                OnPropertyChanged(nameof(ErrorSoundPath));
                OnPropertyChanged(nameof(ErrorSoundHintVisibility));
            }
        };

        BrowseCompleteSoundCommand = new RelayCommand(_ => BrowseSoundFile(path => CompleteSoundPath = path));
        BrowseCancelSoundCommand = new RelayCommand(_ => BrowseSoundFile(path => CancelSoundPath = path));
        BrowseErrorSoundCommand = new RelayCommand(_ => BrowseSoundFile(path => ErrorSoundPath = path));
    }

    public bool Topmost
    {
        get => AppConfig.Instance.Common.Topmost;
        set
        {
            AppConfig.Instance.Common.Topmost = value;
            OnPropertyChanged();
        }
    }

    public bool AutoCheckUpdate
    {
        get => AppConfig.Instance.Common.AutoCheckUpdate;
        set
        {
            AppConfig.Instance.Common.AutoCheckUpdate = value;
            OnPropertyChanged();
        }
    }

    public bool IsMuted
    {
        get => AppConfig.Instance.Common.IsMuted;
        set
        {
            AppConfig.Instance.Common.IsMuted = value;
            OnPropertyChanged();
        }
    }

    public string CompleteSoundPath
    {
        get => AppConfig.Instance.Common.CompleteSoundPath;
        set
        {
            AppConfig.Instance.Common.CompleteSoundPath = value;
            _ = SoundPlayer.PlayAsync(value, "complete.mp3");
            OnPropertyChanged();
            OnPropertyChanged(nameof(CompleteSoundHintVisibility));
        }
    }

    public string CancelSoundPath
    {
        get => AppConfig.Instance.Common.CancelSoundPath;
        set
        {
            AppConfig.Instance.Common.CancelSoundPath = value;
            _ = SoundPlayer.PlayAsync(value, "cancel.mp3");
            OnPropertyChanged();
            OnPropertyChanged(nameof(CancelSoundHintVisibility));
        }
    }

    public string ErrorSoundPath
    {
        get => AppConfig.Instance.Common.ErrorSoundPath;
        set
        {
            AppConfig.Instance.Common.ErrorSoundPath = value;
            _ = SoundPlayer.PlayAsync(value, "error.mp3");
            OnPropertyChanged();
            OnPropertyChanged(nameof(ErrorSoundHintVisibility));
        }
    }

    public System.Windows.Visibility CompleteSoundHintVisibility => string.IsNullOrEmpty(CompleteSoundPath) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility CancelSoundHintVisibility => string.IsNullOrEmpty(CancelSoundPath) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility ErrorSoundHintVisibility => string.IsNullOrEmpty(ErrorSoundPath) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public ICommand BrowseCompleteSoundCommand { get; }

    public ICommand BrowseCancelSoundCommand { get; }

    public ICommand BrowseErrorSoundCommand { get; }

    private static void BrowseSoundFile(Action<string> setPathAction)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Audio Files (*.mp3;*.wav)|*.mp3;*.wav|All Files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
            setPathAction(openFileDialog.FileName);
    }
}