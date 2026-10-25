using Common;
using Microsoft.Win32;
using RomForge.Core;
using RomForge.Core.Models.Switch;
using RomForge.ViewModels.Switch;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.Switch;

public partial class RenameTab : UserControl
{
    private RenameMainViewModel ViewModel => (RenameMainViewModel)DataContext;

    private readonly ListViewColumnSorter _sorter = new();

    public RenameTab() => InitializeComponent();

    private void LvFiles_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel == null)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        await ViewModel.AddPathsAsync(Common.Utils.ExpandPaths(paths));
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
            return;

        ViewModel?.RemoveItems([.. lvFiles.SelectedItems.Cast<RenameFileItem>()]);
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) => _sorter.HandleHeaderClick(e, lvFiles);

    private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "이름을 변경할 파일 선택",
            Multiselect = true,
            Filter = "Switch 파일 (*.nsp;*.xci;*.nsz;*.xcz)|*.nsp;*.xci;*.nsz;*.xcz|모든 파일|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        await ViewModel.AddPathsAsync(dialog.FileNames);
    }

    private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "추가할 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != true)
            return;

        await ViewModel.AddPathsAsync(Common.Utils.ExpandPaths([dialog.SelectedPath]));
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e) => ViewModel?.RemoveItems([.. lvFiles.SelectedItems.Cast<RenameFileItem>()]);

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ViewModel?.ClearItems();

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<RenameFileItem>().FirstOrDefault();

        if (selected == null)
            return;

        Path.GetDirectoryName(selected.FilePath)?.OpenFolder();
    }
}