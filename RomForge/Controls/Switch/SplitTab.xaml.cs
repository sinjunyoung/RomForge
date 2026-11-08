using Common.Models;
using Common.Localization;
using NSW.Core;
using RomForge.Core.Services;
using RomForge.ViewModels.Switch;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls.Switch;

public partial class SplitTab : UserControl
{
    private SplitMainViewModel ViewModel => (SplitMainViewModel)DataContext;

    public SplitTab() => InitializeComponent();

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);

        if (VisualParent == null)
            ViewModel?.Cancel();
    }

    private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = Loc.S.HintSelectOutput,
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            ViewModel.OutputPath = dlg.SelectedPath;
    }

    private async void BtnSplitStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
        {
            ViewModel.Cancel();
            return;
        }

        if (!ViewModel.HasFiles)
        {
            ViewModel.AppendLog(Loc.S.MainErrNoFiles, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (KeySetProvider.Instance.KeySet == null)
        {
            ViewModel.AppendLog(Loc.S.MainErrNoKeys, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        await ViewModel.SplitAsync();
    }

    private void TxtOutputPath_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && Directory.Exists(paths[0]))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void TxtOutputPath_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            {
                string droppedPath = paths[0];
                string? folderPath = Directory.Exists(droppedPath) ? droppedPath : Path.GetDirectoryName(droppedPath);

                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    ViewModel.OutputPath = folderPath;
            }
        }

        e.Handled = true;
    }
}