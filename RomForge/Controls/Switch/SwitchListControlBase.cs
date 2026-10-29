using Common;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using RomForge.Core.Models.Switch;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Res = NSW.Properties.Resources;

namespace RomForge.Controls.Switch;

public class SwitchListControlBase : UserControl
{
    protected virtual ListView? FileListView => null;

    protected virtual string? SelectedFilePath => null;

    protected virtual Task AddPathsAsync(IEnumerable<string> paths) => Task.CompletedTask;

    protected virtual void RemoveSelectedItems() { }

    protected virtual void RemoveAllItems() { }

    protected List<T> GetSelectedItems<T>()
    {
        if (FileListView == null)
            return [];

        return [.. FileListView.SelectedItems.Cast<T>()];
    }

    protected async void BtnAddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = Res.Dialog_SelectGameFile,
            Filter = SwitchFileTypes.OpenFileFilter,
            Multiselect = true
        };

        if (dlg.ShowDialog() == true)
            await AddPathsAsync(Utils.ExpandPaths(dlg.FileNames));
    }

    protected async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new VistaFolderBrowserDialog
        {
            Description = "게임 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            await AddPathsAsync(Utils.ExpandPaths([dlg.SelectedPath]));
    }

    protected void BtnRemoveFile_Click(object sender, RoutedEventArgs e) => RemoveSelectedItems();

    protected void BtnRemoveAllFiles_Click(object sender, RoutedEventArgs e) => RemoveAllItems();

    protected void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            RemoveSelectedItems();
    }

    protected void LvFiles_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    protected async void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        await AddPathsAsync(Utils.ExpandPaths(paths));
    }

    protected void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (FileListView?.SelectedItems.Count == 0)
            e.Handled = true;
    }

    protected void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFilePath is not { } filePath)
            return;

        Path.GetDirectoryName(filePath)?.OpenFolder();
    }
}