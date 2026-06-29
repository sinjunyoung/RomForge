using Microsoft.Win32;
using NSW.WPF.Services;
using RomForge.ViewModels.Switch;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace RomForge.Controls.Switch;

public partial class ConverterTab : UserControl
{
    private ConverterMainViewModel? ViewModel => DataContext as ConverterMainViewModel;

    private string? _lastSortColumn;
    private ListSortDirection _lastSortDirection;

    public ConverterTab()
    {
        InitializeComponent();
        DataContextChanged += ConverterTab_DataContextChanged;
    }

    private void ConverterTab_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ConverterMainViewModel oldVm)
            oldVm.ScrollToItemRequested -= OnScrollToItemRequested;

        if (e.NewValue is ConverterMainViewModel newVm)
            newVm.ScrollToItemRequested += OnScrollToItemRequested;
    }

    private void OnScrollToItemRequested(ConverterFileItem item)
    {
        Dispatcher.InvokeAsync(() => lvFiles.ScrollIntoView(item), DispatcherPriority.Background);
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
        if (ViewModel == null) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        await ViewModel.AddPathsAsync(ExpandPaths(paths));
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        var selected = lvFiles.SelectedItems.Cast<ConverterFileItem>().ToList();
        ViewModel?.RemoveItems(selected);
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header) return;
        if (header.Tag is not string sortBy) return;

        var direction =
            _lastSortColumn == sortBy && _lastSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

        var dataView = CollectionViewSource.GetDefaultView(lvFiles.ItemsSource);
        if (dataView == null) return;

        dataView.SortDescriptions.Clear();
        dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
        dataView.Refresh();

        _lastSortColumn = sortBy;
        _lastSortDirection = direction;
    }

    private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var dialog = new OpenFileDialog
        {
            Title = "변환할 파일 선택",
            Multiselect = true,
            Filter = "Switch 파일 (*.nsp;*.xci;*.nsz;*.xcz)|*.nsp;*.xci;*.nsz;*.xcz|모든 파일|*.*"
        };

        if (dialog.ShowDialog() != true) return;
        await ViewModel.AddPathsAsync(dialog.FileNames);
    }

    private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "추가할 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != true) return;
        await ViewModel.AddPathsAsync(ExpandPaths([dialog.SelectedPath]));
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<ConverterFileItem>().ToList();
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
        var selected = lvFiles.SelectedItems.Cast<ConverterFileItem>().FirstOrDefault();
        if (selected == null) return;
        Path.GetDirectoryName(selected.FilePath)?.OpenFolder();
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        var opts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*.*", opts))
                    yield return f;
            else if (File.Exists(path))
                yield return path;
        }
    }
}