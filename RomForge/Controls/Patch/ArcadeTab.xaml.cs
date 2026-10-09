using Microsoft.Win32;
using RomForge.Core.Models.Patch;
using RomForge.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.Patch;

public partial class ArcadeTab : UserControl
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public ArcadeTab()
    {
        InitializeComponent();
    }

    private void ArcadeSourceDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            if (Path.GetExtension(files[0]).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                ViewModel.PatchVM.ArcadeVM.SourcePath = files[0];
            else
                MessageBox.Show("ZIP 파일만 선택 가능합니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ArcadePatchDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        string path = files[0];
        string ext = Path.GetExtension(path);

        if (Directory.Exists(path) || ext.Equals(".ips", StringComparison.OrdinalIgnoreCase) || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            ViewModel.PatchVM.ArcadeVM.PatchPath = path;
        else
            MessageBox.Show("폴더 또는 IPS/ZIP 파일만 등록할 수 있습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ArcadePatchDrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.ContextMenu != null)
        {
            element.ContextMenu.PlacementTarget = element;
            element.ContextMenu.IsOpen = true;
        }
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "패치 폴더 선택"
        };

        if (dialog.ShowDialog() == true)
            ViewModel.PatchVM.ArcadeVM.PatchPath = dialog.FolderName;
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "패치 파일 (*.ips;*.zip)|*.ips;*.zip|IPS 파일 (*.ips)|*.ips|ZIP 압축 파일 (*.zip)|*.zip|모든 파일 (*.*)|*.*",
            Title = "패치 파일 선택"
        };

        if (dialog.ShowDialog() == true)
            ViewModel.PatchVM.ArcadeVM.PatchPath = dialog.FileName;
    }

    private void MatchCard_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void MatchCard_PatchDrop(object sender, DragEventArgs e)
    {
        if (sender is not Border border)
            return;

        if (border.Tag is not ArcadeMatchItem item)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        string path = files[0];
        var patchEntry = new PatchEntry
        {
            DisplayName = Path.GetFileName(path),
            EntryPath = path
        };

        ViewModel.PatchVM.ArcadeVM.ManualMatch(item, patchEntry);
    }

    private void ArcadeSourceDrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "ZIP 압축 파일 (*.zip)|*.zip",
            Title = "원본 ZIP 파일 선택"
        };

        if (openFileDialog.ShowDialog() == true)
            ViewModel.PatchVM.ArcadeVM.SourcePath = openFileDialog.FileName;
    }

    private void PatchPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo)
            return;

        if (combo.Tag is not ArcadeMatchItem item)
            return;

        if (combo.SelectedItem is not PatchEntry entry)
            return;

        if (ReferenceEquals(entry, item.PatchEntry))
            return;

        ViewModel.PatchVM.ArcadeVM.ManualMatch(item, entry);
    }
}