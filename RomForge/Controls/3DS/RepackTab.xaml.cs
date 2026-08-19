using Common.WPF;
using NSW.Core.Enums;
using RomForge.ViewModels._3DS;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls._3DS
{
    public partial class RepackTab : UserControl
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".3ds", ".cci", ".zcci", ".cia" };

        RepackMainViewModel ViewModel => (RepackMainViewModel)DataContext;

        public RepackTab()
        {
            InitializeComponent();
        }

        private void TxtRom_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    string extension = Path.GetExtension(filePath);

                    if (SupportedExtensions.Contains(extension))
                        ViewModel.InputPath = filePath;
                }
            }

            e.Handled = true;
        }

        private void TxtPatch_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void TxtPatch_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void TxtPatch_Drop(object sender, DragEventArgs e)
        {
            var items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            var path = items?.FirstOrDefault(PatchDropValidator.IsValidPatchPath);

            if (path != null)
                ViewModel.PatchPath = path;

            e.Handled = true;
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsLocked) 
            {
                ViewModel.Cancel(); 
                return;
            }
            
            await ViewModel.StartAsync(BuildMode.FullProcess);
        }

        private async void BtnUnpack_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsLocked)
            {
                ViewModel.Cancel();
                return;
            }

            await ViewModel.StartAsync(BuildMode.UnpackOnly);
        }

        private void BtnRebuild_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsLocked) 
            { 
                ViewModel.Cancel();
                return; 
            }

            _ = ViewModel.StartAsync(BuildMode.RebuildOnly);
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://sinjunyoung.github.io/RomForge/3ds-merge/",
                UseShellExecute = true
            };

            System.Diagnostics.Process.Start(psi);
        }
    }
}