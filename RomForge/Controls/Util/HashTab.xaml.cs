using Microsoft.Win32;
using NSW.WPF.Services;
using RomForge.Models;
using RomForge.ViewModels.Util;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RomForge.Controls.Util;

public partial class HashTab : UserControl
{
    private HashMainViewModel ViewModel => (HashMainViewModel)DataContext;

    public HashTab()
    {
        InitializeComponent();
        DataContextChanged += HashTab_DataContextChanged;
    }

    private void HashTab_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ScrollToItemRequested -= OnScrollToItemRequested;
            ViewModel.ScrollToItemRequested += OnScrollToItemRequested;
        }
    }

    private void OnScrollToItemRequested(HashFileItem item)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (item != null)
                lvFiles.ScrollIntoView(item);
        }, DispatcherPriority.Background);
    }

    private void RadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is RadioButton rb && rb.Tag is string tag && ViewModel != null)
        {
            if (Enum.TryParse<HashAlgorithmType>(tag, out var algo))
                ViewModel.SelectedAlgorithm = algo;
        }
    }

    private void LvFiles_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await ViewModel.AddPaths(paths);
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            var selected = lvFiles.SelectedItems.Cast<HashFileItem>().ToList();
            ViewModel.RemoveItems(selected);
        }

        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            CopySelectedHashes();
            e.Handled = true;
        }
    }

    private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = HashMainViewModel.GetFileDialogFilter()
        };

        if (dlg.ShowDialog() == true)
            await ViewModel.AddPaths(dlg.FileNames);
    }

    private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "추가할 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            await ViewModel.AddPaths([dlg.SelectedPath]);
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<HashFileItem>().ToList();
        ViewModel.RemoveItems(selected);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ViewModel.ClearItems();

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_CopyHash_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedHashes();
    }

    private void CopySelectedHashes()
    {
        var selected = lvFiles.SelectedItems.Cast<HashFileItem>()
            .Where(item => !string.IsNullOrEmpty(item.HashResult))
            .Select(item => item.HashResult)
            .ToList();

        if (selected.Count == 0)
            return;

        string textToCopy = string.Join(Environment.NewLine, selected);
        Clipboard.SetText(textToCopy);
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<HashFileItem>().ToList();

        if (selected.Count == 0) 
            return;

        string? dir = Path.GetDirectoryName(selected[0].FilePath);

        dir?.OpenFolder();
    }
}