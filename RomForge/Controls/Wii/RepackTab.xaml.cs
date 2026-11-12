using Common.Models;
using Common.Localization;
using Microsoft.Win32;
using RomForge.Core.Services.Wii;
using RomForge.ViewModels.Wii;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.Wii;

public partial class RepackTab : UserControl
{
    private RepackMainViewModel ViewModel => (RepackMainViewModel)DataContext;

    public RepackTab() => InitializeComponent();

    private void Drag_Over(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void TxtRom_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (!ViewModel.IsIdle)
            return;

        var items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var path = items?.FirstOrDefault(RepackMainViewModel.IsSupportedDisc);

        if (path != null)
            ViewModel.InputPath = path;
    }

    private void TxtPatch_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (!ViewModel.IsIdle)
            return;

        var items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var path = items?.FirstOrDefault(RiivolutionWorkspace.IsSupported);

        if (path != null)
            ViewModel.PatchPath = path;
    }

    private void TxtPatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.ContextMenu != null)
        {
            element.ContextMenu.PlacementTarget = element;
            element.ContextMenu.IsOpen = true;
        }
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = Loc.S.DialogSelectPatchFolder
        };

        if (dialog.ShowDialog() == true)
            ViewModel.PatchPath = dialog.FolderName;
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{Loc.S.FilterPatchFiles} (*.zip;*.7z;*.xml)|*.zip;*.7z;*.xml|{Loc.S.FilterFilesOfType("ZIP")} (*.zip)|*.zip|{Loc.S.FilterFilesOfType("7Z")} (*.7z)|*.7z|{Loc.S.FilterFilesOfType("XML")} (*.xml)|*.xml|{Loc.S.FilterAllFiles} (*.*)|*.*",
            Title = Loc.S.DialogSelectPatchFile
        };

        if (dialog.ShowDialog() == true)
            ViewModel.PatchPath = dialog.FileName;
    }

    private void TxtRom_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = $"{Loc.S.FilterWiiDiscFiles}|*.iso;*.wbfs;*.rvz;*.wia|{Loc.S.FilterAllFiles}|*.*",
            Title = Loc.S.DialogSelectWiiDiscFile
        };

        if (dlg.ShowDialog() == true)
            ViewModel.InputPath = dlg.FileName;
    }

    private async void BtnUnpack_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
        {
            ViewModel.Cancel();
            return;
        }

        await ViewModel.StartAsync(BuildMode.UnpackOnly);
    }

    private async void BtnRebuild_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
        {
            ViewModel.Cancel();
            return;
        }

        await ViewModel.StartAsync(BuildMode.RebuildOnly);
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
        {
            ViewModel.Cancel();
            return;
        }

        await ViewModel.StartAsync(BuildMode.FullProcess);
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