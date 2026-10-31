using RomForge.Core.Services;
using RomForge.ViewModels.Switch;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Res = NSW.Properties.Resources;

namespace RomForge.Controls.Switch;

public partial class Lz4MergeTab : UserControl
{
    private Lz4MergeMainViewModel ViewModel => (Lz4MergeMainViewModel)DataContext;

    public Lz4MergeTab() => InitializeComponent();

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
            Description = Res.Hint_SelectOutput,
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            ViewModel.OutputPath = dlg.SelectedPath;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
        {
            ViewModel.Cancel();
            return;
        }

        if (!fileMgr.GameFiles.Any())
        {
            ViewModel.AppendLog(Res.Main_Err_NoFiles, Common.LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (!CommonListControl.KeyExists())
        {
            ViewModel.AppendLog(Res.Main_Err_NoKeys, Common.LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (fileMgr.GameFiles.Any(f => f.IsKeyMissing))
            await RecalcKeyMissingAsync();

        await ViewModel.StartAsync(fileMgr.GameFiles);
    }

    private Task RecalcKeyMissingAsync()
    {
        var tcs = new TaskCompletionSource();

        fileMgr.RecalcKeyMissingFiles(() => tcs.SetResult());

        return tcs.Task;
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