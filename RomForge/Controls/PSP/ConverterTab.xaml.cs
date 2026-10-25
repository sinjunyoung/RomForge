using Common;
using Common.WPF;
using NSW.Core.Services;
using RomForge.Core.Models.PSP;
using RomForge.ViewModels.PSP;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RomForge.Controls.PSP;

public partial class ConverterTab : UserControl
{
    private ConverterViewModel ViewModel => (ConverterViewModel)DataContext;

    public ConverterTab()
    {
        InitializeComponent();
        DataContextChanged += ConverterTab_DataContextChanged;
    }

    private void ConverterTab_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ConverterViewModel oldVm)
            oldVm.ScrollToItemRequested -= OnScrollToItemRequested;

        if (e.NewValue is ConverterViewModel newVm)
            newVm.ScrollToItemRequested += OnScrollToItemRequested;
    }

    private void OnScrollToItemRequested(FileItem item) => Dispatcher.InvokeAsync(() => lvFiles.ScrollIntoView(item), DispatcherPriority.Background);

    private void LvFiles_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel == null) 
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) 
            return;

        ViewModel.AddPaths(paths);
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
            return;

        var selected = lvFiles.SelectedItems.Cast<FileItem>().ToList();

        ViewModel?.RemoveItems(selected);
    }

    private readonly ListViewColumnSorter _sorter = new();

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) => _sorter.HandleHeaderClick(e, lvFiles);

    private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) 
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "변환할 파일 선택",
            Multiselect = true,
            Filter = "PS 파일|*.iso;*.cso;*.zso;*.chd|모든 파일|*.*"
        };

        if (dialog.ShowDialog() == true)
            ViewModel.AddPaths(dialog.FileNames);
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "추가할 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == true)
            ViewModel.AddPaths([dialog.SelectedPath]);
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<FileItem>().ToList();

        ViewModel?.RemoveItems(selected);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ViewModel?.ClearItems();

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<FileItem>().ToList();

        if (selected.Count == 0) 
            return;

        Path.GetDirectoryName(selected[0].FilePath)?.OpenFolder();
    }
}