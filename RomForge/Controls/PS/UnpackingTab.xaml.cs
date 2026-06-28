using NSW.WPF.Services;
using RomForge.ViewModels.PS;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace RomForge.Controls.PS;

public partial class UnpackingTab : UserControl
{
    private UnpackingMainViewModel? ViewModel => (UnpackingMainViewModel)DataContext;

    public UnpackingTab()
    {
        InitializeComponent();

        DataContextChanged += ConverterTab_DataContextChanged;
    }

    private void ConverterTab_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is UnpackingMainViewModel oldVm)
            oldVm.ScrollToItemRequested -= OnScrollToItemRequested;

        if (e.NewValue is UnpackingMainViewModel newVm)
            newVm.ScrollToItemRequested += OnScrollToItemRequested;
    }

    private void OnScrollToItemRequested(PbpFileItem item)
    {
        Dispatcher.InvokeAsync(() =>
        {
            lvFiles.ScrollIntoView(item);
        }, DispatcherPriority.Background);
    }

    private void LvFiles_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel == null)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        await ViewModel.AddPaths(paths);
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
            return;

        var selected = lvFiles.SelectedItems.Cast<PbpFileItem>().ToList();
        ViewModel?.RemoveItems(selected);
    }

    private string? _lastSortColumn;
    private ListSortDirection _lastSortDirection;

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header)
            return;

        if (header.Tag is not string sortBy)
            return;

        var direction =
            _lastSortColumn == sortBy &&
            _lastSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

        ICollectionView dataView = CollectionViewSource.GetDefaultView(lvFiles.ItemsSource);

        if (dataView == null)
            return;

        dataView.SortDescriptions.Clear();
        dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
        dataView.Refresh();

        _lastSortColumn = sortBy;
        _lastSortDirection = direction;
    }

    private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "파일 선택",
            Multiselect = true,
            Filter = "PBP 파일|*.pbp|모든 파일|*.*"
        };

        if (dialog.ShowDialog() == true)
            await ViewModel.AddPaths(dialog.FileNames);
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

        if (dialog.ShowDialog() == true)
            await ViewModel.AddPaths([dialog.SelectedPath]);
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<PbpFileItem>().ToList();
        ViewModel?.RemoveItems(selected);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ClearItems();
    }

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<PbpFileItem>().ToList();

        if (selected.Count == 0)
            return;

        string? dir = Path.GetDirectoryName(selected[0].FilePath);

        dir?.OpenFolder();
    }
}