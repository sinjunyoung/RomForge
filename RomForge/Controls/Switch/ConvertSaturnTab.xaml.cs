using System;
using System.Windows;
using System.Windows.Controls;
using RomForge.ViewModels.Switch;

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

        private async void BtnStart_Click(object sender, RoutedEventArgs e) => await ViewModel.ConvertAsync();
    }
}