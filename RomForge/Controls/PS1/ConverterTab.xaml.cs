using RomForge.ViewModels.PS1;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.PS1;

public partial class ConverterTab : UserControl
{
    private readonly string[] _imgExts = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];

    private ConverterMainViewModel? ViewModel => DataContext as ConverterMainViewModel;

    public ConverterTab()
    {
        InitializeComponent();
    }

    private void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        ViewModel?.AddPaths(paths);
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
            return;

        var selected = lvFiles.SelectedItems.Cast<DiscFileItem>().ToList();
        ViewModel?.RemoveItems(selected);
    }

    private void Icon0_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);

        if (files is not { Length: > 0 })
            return;

        string ext = Path.GetExtension(files[0]).ToLowerInvariant();

        if (!_imgExts.Contains(ext))
            return;

        byte[] rawBytes = File.ReadAllBytes(files[0]);
        ViewModel?.SetIcon0FromBytes(rawBytes);
    }

    private void Pic0_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);

        if (files is not { Length: > 0 })
            return;

        string ext = Path.GetExtension(files[0]).ToLowerInvariant();

        if (!_imgExts.Contains(ext))
            return;

        byte[] rawBytes = File.ReadAllBytes(files[0]);
        ViewModel?.SetPic0FromBytes(rawBytes);
    }

    private void Pic1_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);

        if (files is not { Length: > 0 })
            return;

        string ext = Path.GetExtension(files[0]).ToLowerInvariant();

        if (!_imgExts.Contains(ext))
            return;

        byte[] rawBytes = File.ReadAllBytes(files[0]);
        ViewModel?.SetPic1FromBytes(rawBytes);
    }

    private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = ConverterMainViewModel.GetFileDialogFilter()
        };

        if (dialog.ShowDialog() == true)
            ViewModel?.AddPaths(dialog.FileNames);
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "추가할 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == true)
            ViewModel?.AddPaths([dialog.SelectedPath]);
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<DiscFileItem>().ToList();
        ViewModel?.RemoveItems(selected);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ClearItems();
    }
}