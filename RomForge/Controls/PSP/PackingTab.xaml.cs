using Common;
using RomForge.Core.Models.PSP;
using RomForge.ViewModels.PSP;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RomForge.Controls.PSP;

public partial class PackingTab : UserControl
{
    private readonly string[] _imgExts = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];
    private readonly string[] _audioExts = [".mp3", ".wav", ".at3"];

    private PackingMainViewModel ViewModel => (PackingMainViewModel)DataContext;

    public PackingTab() => InitializeComponent();

    private void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        ViewModel?.Files.AddPaths(paths);
    }

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
            return;

        var selected = lvFiles.SelectedItems.Cast<DiscFileItem>().ToList();

        ViewModel?.Files.Remove(selected);
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

        ViewModel?.Images.SetIcon0FromBytes(rawBytes);
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

        ViewModel?.Images.SetPic0FromBytes(rawBytes);
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

        ViewModel?.Images.SetPic1FromBytes(rawBytes);
    }

    private void Icon0_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ExportImage(ViewModel?.Images.Icon0Image, "Cover.PNG");

    private void Pic0_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ExportImage(ViewModel?.Images.Pic0Image, "Logo.PNG");

    private void Pic1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ExportImage(ViewModel?.Images.Pic1Image, "Background.PNG");

    private void BaseImage_Reset_Click(object sender, RoutedEventArgs e) => ViewModel?.Images.ResetBaseImages();

    private void BootLogo_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);

        if (files is not { Length: > 0 })
            return;

        string ext = Path.GetExtension(files[0]).ToLowerInvariant();

        if (!_imgExts.Contains(ext))
            return;

        byte[] rawBytes = File.ReadAllBytes(files[0]);

        ViewModel?.Images.SetBootLogoFromBytes(rawBytes);
    }

    private void BootLogo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.Images.BootLogoImage != null)
            ExportImage(ViewModel.Images.BootLogoImage, "BootLogo.PNG");
    }

    private void BootLogo_Reset_Click(object sender, RoutedEventArgs e) => ViewModel?.Images.ResetBootLogo();

    private void Bgm_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);

        if (files is not { Length: > 0 })
            return;

        ProcessAudioFile(files[0]);
    }

    private void Bgm_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is { Bgm.IsConverting: true })
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Audio Files (*.mp3;*.wav;*.at3)|*.mp3;*.wav;*.at3|MP3 Files (*.mp3)|*.mp3|WAV Files (*.wav)|*.wav|AT3 Files (*.at3)|*.at3"
        };

        if (dialog.ShowDialog() == true)
            ProcessAudioFile(dialog.FileName);
    }

    private void BgmPlayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void BgmSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e) => ViewModel?.Bgm.Preview.BeginSeek();

    private void BgmSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) => ViewModel?.Bgm.Preview.EndSeek();

    private void Bgm_Reset_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { Bgm.IsConverting: true })
            return;

        ViewModel?.Bgm.Reset();
    }

    private async void ProcessAudioFile(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (!_audioExts.Contains(ext))
            return;

        if (ViewModel != null)
            await ViewModel.Bgm.SetPathAsync(filePath);
    }

    private void ExportImage(BitmapImage? bmp, string fileNameBase)
    {
        if (bmp == null)
            return;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string safeGameTitle = !string.IsNullOrEmpty(ViewModel?.GameTitle) ? string.Join("_", ViewModel.GameTitle.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)) : string.Empty;
        string fileName = !string.IsNullOrEmpty(safeGameTitle) ? $"{safeGameTitle}_{fileNameBase}" : fileNameBase;
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
                try 
                {
                    File.Delete(tempFilePath);
                } 
                catch { }
            }
        }
    }

    private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = DiscListViewModel.GetFileDialogFilter()
        };

        if (dialog.ShowDialog() == true)
            ViewModel?.Files.AddPaths(dialog.FileNames);
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "추가할 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == true)
            ViewModel?.Files.AddPaths([dialog.SelectedPath]);
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedItems.Cast<DiscFileItem>().ToList();

        ViewModel?.Files.Remove(selected);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ViewModel?.Files.Clear();

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

        Path.GetDirectoryName(selected[0].FilePath)?.OpenFolder();
    }
}