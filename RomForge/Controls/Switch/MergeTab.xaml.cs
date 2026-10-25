using NSW.Core.UI;
using RomForge.Core.Services;
using RomForge.ViewModels.Switch;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.Controls.Switch;

public partial class MergeTab : UserControl
{
    private MergeMainViewModel ViewModel => (MergeMainViewModel)DataContext;

    public MergeTab() => InitializeComponent();

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://sinjunyoung.github.io/RomForge/switch-merge/",
            UseShellExecute = true
        };

        System.Diagnostics.Process.Start(psi);
    }

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

    private async void BtnMergeStart_Click(object sender, RoutedEventArgs e)
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

        await ViewModel.MergeAsync(fileMgr.GameFiles);
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