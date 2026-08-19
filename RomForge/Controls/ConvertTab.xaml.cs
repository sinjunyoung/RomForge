using Common.WPF;
using Microsoft.Win32;
using NSW.WPF.Services;
using RomForge.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RomForge.Controls;

public partial class ConvertTab : UserControl
{
    private readonly ListViewColumnSorter _sorter = new();

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public ConvertTab()
    {
        InitializeComponent();

        DataContextChanged += UnifiedConvertTab_DataContextChanged;
    }

    private void UnifiedConvertTab_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ViewModel?.UnifiedConvertVM != null)
        {
            ViewModel.UnifiedConvertVM.ScrollToItemRequested -= OnScrollToItemRequested;
            ViewModel.UnifiedConvertVM.ScrollToItemRequested += OnScrollToItemRequested;
        }
    }

    private void OnScrollToItemRequested(object item)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (item != null)
                lvFiles.ScrollIntoView(item);
        }, DispatcherPriority.Background);
    }

    private void LvFiles_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await ViewModel.UnifiedConvertVM.AddPaths(paths);
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
            return;

        ViewModel.UnifiedConvertVM.RemoveItems([.. lvFiles.SelectedItems.Cast<object>()]);
    }

    private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "지원 파일|*.nsp;*.xci;*.cci;*.3ds;*.cia;*.wud;*.wux;*.wua;*.mds;*.ccd;*.pbp|모든 파일|*.*"
        };

        if (dlg.ShowDialog() == true)
            await ViewModel.UnifiedConvertVM.AddPaths(dlg.FileNames);
    }

    private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "추가할 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            await ViewModel.UnifiedConvertVM.AddPaths([dlg.SelectedPath]);
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e) => ViewModel.UnifiedConvertVM.RemoveItems([.. lvFiles.SelectedItems.Cast<object>()]);

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ViewModel.UnifiedConvertVM.ClearItems();

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            return;

        if (lvFiles.SelectedItems[0] is not Common.WPF.ViewModels.FileItemBase item)
            return;

        Path.GetDirectoryName(item.FilePath)?.OpenFolder();
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) => _sorter.HandleHeaderClick(e, lvFiles);
}