using Common;
using NSW.Core.Enums;
using NSW.Core.Services;
using Ookii.Dialogs.Wpf;
using RomForge.ViewModels.PSVita;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.PSVita;

public partial class RepackTab : UserControl
{
    private RepackMainViewModel ViewModel => (RepackMainViewModel)DataContext;

    public RepackTab() => InitializeComponent();

    private void LvPkg_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var selected = ViewModel?.SelectedRow;

        if (selected == null)
            e.Handled = true;
    }

    private void Root_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void Root_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                foreach (var file in files)
                    ViewModel?.AddSourceFile(file);

        e.Handled = true;
    }

    private void LvPkg_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            if (ViewModel?.RemoveSelectedCommand?.CanExecute(null) == true)
                ViewModel.RemoveSelectedCommand.Execute(null);
    }

    private void License_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void License_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                if (sender is TextBox tb && tb.DataContext is SourceRowViewModel row)
                    row.License = files[0];

        e.Handled = true;
    }

    private void PatchDropTarget_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void PatchDropTarget_DragLeave(object sender, DragEventArgs e) => e.Handled = true;

    private void PatchDropTarget_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                string path = files[0];

                if (Directory.Exists(path) || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                    if (sender is FrameworkElement { Tag: SourceRowViewModel row })
                        row.PatchPath = path;
            }

        e.Handled = true;
    }

    private void PatchDropTarget_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
            if (element.ContextMenu != null)
            {
                element.ContextMenu.PlacementTarget = element;
                element.ContextMenu.IsOpen = true;
            }
    }

    private void PatchMenu_SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is SourceRowViewModel rowVm)
        {
            var dlg = new VistaFolderBrowserDialog
            {
                Description = "한글 패치 폴더 선택",
                UseDescriptionForTitle = true
            };

            if (dlg.ShowDialog() == true)
                rowVm.PatchPath = dlg.SelectedPath;
        }
    }

    private void PatchMenu_SelectArchive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is SourceRowViewModel rowVm)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "한글 패치 압축파일 선택",
                Filter = "Archive Files (*.zip;*.7z)|*.zip;*.7z|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
                rowVm.PatchPath = dlg.FileName;
        }
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel?.SelectedRow;

        Path.GetDirectoryName(selected?.Path)?.OpenFolder();
    }

    private void MenuItem_RemovePatch_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel?.SelectedRow;

        if (selected != null)
            selected.PatchPath = string.Empty;
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
                    if (ViewModel != null)
                        ViewModel.OutputPath = folderPath;
            }
        }

        e.Handled = true;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e) => await RunAsync(BuildMode.FullProcess);

    private async void BtnUnpack_Click(object sender, RoutedEventArgs e) => await RunAsync(BuildMode.UnpackOnly);

    private async void BtnRebuild_Click(object sender, RoutedEventArgs e) => await RunAsync(BuildMode.RebuildOnly);

    private async Task RunAsync(BuildMode mode)
    {
        if (ViewModel.IsLocked)
        {
            ViewModel.Cancel();
            return;
        }

        await ViewModel.StartAsync(mode);
    }
}