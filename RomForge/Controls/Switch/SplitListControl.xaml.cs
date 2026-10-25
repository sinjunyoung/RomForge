using Common;
using Microsoft.Win32;
using RomForge.ViewModels.Switch;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Res = NSW.Properties.Resources;

namespace RomForge.Controls.Switch;

public partial class SplitListControl : UserControl
{
    private SplitMainViewModel ViewModel => (SplitMainViewModel)DataContext;

    public SplitListControl() => InitializeComponent();

    private void BtnAddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = Res.Dialog_SelectGameFile,
            Filter = $"{Res.Filter_SwitchFiles} (*.nsp;*.xci;*.nsz;*.xcz)|*.nsp;*.xci;*.nsz;*.xcz|{Res.Filter_AllFiles}|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog() == true && ViewModel != null)
            _ = ViewModel.AddFilesAsync(Common.Utils.ExpandPaths(dlg.FileNames));
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "게임 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true && ViewModel != null)
            _ = ViewModel.AddFilesAsync(Common.Utils.ExpandPaths([dlg.SelectedPath]));
    }

    private void BtnRemoveFile_Click(object sender, RoutedEventArgs e) => ViewModel?.RemoveRows([.. lvFiles.SelectedItems.Cast<SplitRowViewModel>()]);

    private void BtnRemoveAllFiles_Click(object sender, RoutedEventArgs e) => ViewModel?.ClearAll();

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e) => ViewModel?.SelectAll(true);

    private void BtnSelectNone_Click(object sender, RoutedEventArgs e) => ViewModel?.SelectAll(false);

    private void BtnExceptBase_Click(object sender, RoutedEventArgs e) => ViewModel?.SelectExceptBase();

    private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            bool isChecked = checkBox.IsChecked == true;

            ViewModel?.SelectAll(isChecked);
        }
    }

    private void BtnExpand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SplitRowViewModel row })
            ViewModel?.ToggleExpand(row);

        e.Handled = true;
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            BtnRemoveFile_Click(sender, new RoutedEventArgs());
    }

    private void LvFiles_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || ViewModel == null)
            return;

        await ViewModel.AddFilesAsync(Common.Utils.ExpandPaths(paths));
    }

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (lvFiles.SelectedItem is not SplitRowViewModel selected)
            return;

        Path.GetDirectoryName(selected.FilePath)?.OpenFolder();
    }
}