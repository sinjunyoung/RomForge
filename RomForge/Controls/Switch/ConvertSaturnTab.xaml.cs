using RomForge.ViewModels.Switch;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RomForge.Controls.Switch
{
    public partial class ConvertSaturnTab : UserControl
    {
        ConvertSaturnMainViewModel ViewModel => (ConvertSaturnMainViewModel)DataContext;

        public ConvertSaturnTab() => InitializeComponent();

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Cue_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Length > 0 && files[0].EndsWith(".cue", StringComparison.OrdinalIgnoreCase))
                ViewModel.CuePath = files[0];
        }

        private void Nsp_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Length > 0 && (files[0].EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || files[0].EndsWith(".nsz", StringComparison.OrdinalIgnoreCase)))
                ViewModel.NspPath = files[0];
        }

        private void Cover_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Length > 0)
                ViewModel.CoverImagePath = files[0];
        }

        private void ImgCover_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (imgCover.Source is not BitmapSource bitmapSource)
                return;

            string fileName = $"{ViewModel.GameTitle}_Saturn.png";

            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);

            try
            {
                using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    encoder.Save(fs);
                }

                var data = new DataObject();
                data.SetFileDropList([tempFilePath]);
                DragDrop.DoDragDrop(imgCover, data, DragDropEffects.Copy);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    try { File.Delete(tempFilePath); }
                    catch { }
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsLocked)
            {
                ViewModel.Cancel();
                return;
            }

            await ViewModel.ConvertAsync();
        }
    }
}