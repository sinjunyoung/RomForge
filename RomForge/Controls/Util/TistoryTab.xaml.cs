using Common;
using Common.WPF;
using NSW.Core.Services;
using RomForge.Core.Models.Util;
using RomForge.ViewModels.Util;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RomForge.Controls.Util;

public partial class TistoryTab : UserControl
{
    private readonly ListViewColumnSorter _sorter = new();

    private TistoryMainViewModel ViewModel => (TistoryMainViewModel)DataContext;

    public TistoryTab()
    {
        InitializeComponent();
        DataContextChanged += TistoryTab_DataContextChanged;
    }

    private void TistoryTab_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ScrollToItemRequested -= OnScrollToItemRequested;
            ViewModel.ScrollToItemRequested += OnScrollToItemRequested;
        }
    }

    private void OnScrollToItemRequested(TistoryDownloadItem item) => Dispatcher.InvokeAsync(() => lvFiles.ScrollIntoView(item), DispatcherPriority.Background);

    private void TxtUrl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.Control)
            return;

        e.Handled = true;

        if (ViewModel.AnalyzeCommand.CanExecute(null))
            ViewModel.AnalyzeCommand.Execute(null);
    }

    private void BtnChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "다운로드 파일을 저장할 폴더를 선택하세요",
            UseDescriptionForTitle = true,
            SelectedPath = ViewModel.SaveDirectory
        };

        if (dlg.ShowDialog() == true)
            ViewModel.ChangeSaveDirectory(dlg.SelectedPath);
    }

    private void TxtSaveDirectory_PreviewDragOver(object sender, DragEventArgs e)
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

    private void TxtSaveDirectory_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            {
                string droppedPath = paths[0];
                string? folderPath = Directory.Exists(droppedPath) ? droppedPath : Path.GetDirectoryName(droppedPath);

                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    ViewModel?.ChangeSaveDirectory(folderPath);
            }
        }

        e.Handled = true;
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
            return;

        var selected = lvFiles.SelectedItems.Cast<TistoryDownloadItem>().ToList();

        ViewModel.RemoveItems(selected);
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<TistoryDownloadItem>().ToList();

        ViewModel.RemoveItems(selected);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ViewModel.ClearItems();

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<TistoryDownloadItem>().ToList();

        if (selected.Count == 0)
            return;

        string? dir = selected[0].SavedPath != null ? Path.GetDirectoryName(selected[0].SavedPath) : ViewModel.SaveDirectory;

        dir?.OpenFolder();
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) => _sorter.HandleHeaderClick(e, lvFiles);

    private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox headerCheckBox && lvFiles.ItemsSource is IEnumerable<TistoryDownloadItem> items)
        {
            bool isChecked = headerCheckBox.IsChecked == true;
            foreach (var item in items)
            {
                item.IsSelected = isChecked;
            }
        }
    }
}