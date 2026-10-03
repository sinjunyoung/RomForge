using Common.WPF.ViewModels;
using RomForge.Core;
using RomForge.Core.UI.Command;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Settings;

public class PatchSettingsMainViewModel : ToolTabViewModel
{
    public PatchSettingsMainViewModel()
    {
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());

        AppConfig.Instance.Patch.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PatchConfig.AutoCompress))
                OnPropertyChanged(nameof(AutoCompress));
            else if (e.PropertyName == nameof(PatchConfig.UseNormalPatchCustomOutputPath))
            {
                OnPropertyChanged(nameof(UseCustomOutputPath));
                OnPropertyChanged(nameof(OutputPathVisibility));
            }
            else if (e.PropertyName == nameof(PatchConfig.OutputPath))
            {
                OnPropertyChanged(nameof(OutputPath));
                OnPropertyChanged(nameof(OutputHintVisibility));
            }
        };
    }

    public ICommand BrowseOutputCommand { get; }

    public bool AutoCompress
    {
        get => AppConfig.Instance.Patch.AutoCompress;
        set
        {
            AppConfig.Instance.Patch.AutoCompress = value;
            OnPropertyChanged();
        }
    }

    public bool UseCustomOutputPath
    {
        get => AppConfig.Instance.Patch.UseNormalPatchCustomOutputPath;
        set
        {
            if (value && string.IsNullOrWhiteSpace(AppConfig.Instance.Patch.OutputPath))
                AppConfig.Instance.Patch.OutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

            AppConfig.Instance.Patch.UseNormalPatchCustomOutputPath = value;
            OnPropertyChanged();
        }
    }

    public string? OutputPath
    {
        get => AppConfig.Instance.Patch.OutputPath;
        set
        {
            AppConfig.Instance.Patch.OutputPath = value;
            OnPropertyChanged();
        }
    }

    public Visibility OutputPathVisibility => UseCustomOutputPath ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OutputHintVisibility => string.IsNullOrWhiteSpace(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    private void BrowseOutput()
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "출력 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.SelectedPath;
    }
}