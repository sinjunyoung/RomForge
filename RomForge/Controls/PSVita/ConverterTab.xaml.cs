using Common;
using NSW.Core.Services;
using RomForge.ViewModels.PSVita;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.PSVita;

public partial class ConverterTab : UserControl
{
    private ConverterMainViewModel ViewModel => (ConverterMainViewModel)DataContext;

    public ConverterTab() => InitializeComponent();

    private void LvPkg_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (ViewModel?.SelectedRow == null)
            e.Handled = true;
    }

    private void Root_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (ViewModel != null && e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            await ViewModel.AddPathsAsync(files);
    }

    private void LvPkg_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && ViewModel?.RemoveSelectedCommand?.CanExecute(null) == true)
            ViewModel.RemoveSelectedCommand.Execute(null);
    }

    private void License_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void License_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            if (sender is TextBox { DataContext: SourceRowViewModel row })
                row.License = files[0];
        }

        e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e) => Path.GetDirectoryName(ViewModel?.SelectedRow?.Path)?.OpenFolder();

    private void TxtOutputPath_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && Directory.Exists(paths[0]))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void TxtOutputPath_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            string droppedPath = paths[0];
            string? folderPath = Directory.Exists(droppedPath) ? droppedPath : Path.GetDirectoryName(droppedPath);

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath) && ViewModel != null)
                ViewModel.OutputPath = folderPath;
        }

        e.Handled = true;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
            ViewModel.Cancel();
        else
            await ViewModel.StartAsync();
    }
}