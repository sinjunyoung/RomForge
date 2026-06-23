using NSW.WPF.Services;
using RomForge.ViewModels.PS1;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RomForge.Controls.PS1;

public partial class PackingTab : UserControl
{
    private readonly string[] _imgExts = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];

    private PackingMainViewModel? ViewModel => DataContext as PackingMainViewModel;

    public PackingTab()
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


    private void Icon0_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ExportImage(ViewModel?.Icon0Image,  "Cover.PNG");
    }

    private void Pic0_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ExportImage(ViewModel?.Pic0Image, "Logo.PNG");
    }

    private void Pic1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ExportImage(ViewModel?.Pic1Image, "Background.PNG");
    }

    private void ExportImage(BitmapImage bmp, string fileNameBase)
    {
        string fileName = !string.IsNullOrEmpty(ViewModel?.GameTitle) ? $"{ViewModel.GameTitle}_{fileNameBase}" : fileNameBase;
        string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);

        using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            encoder.Save(fs);
        }

        try
        {
            var dataObject = new DataObject();
            dataObject.SetFileDropList([tempFilePath]);
            DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
        }
    }

    private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = PackingMainViewModel.GetFileDialogFilter()
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

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<DiscFileItem>().ToList();

        if (selected.Count == 0)
            return;

        string? dir = Path.GetDirectoryName(selected[0].FilePath);

        dir?.OpenFolder();
    }
}