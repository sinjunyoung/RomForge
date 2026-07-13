using NSW.Core.Enums;
using RomForge.ViewModels.WiiU;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.WiiU
{
    public partial class RepackTab : UserControl
    {
        RepackMainViewModel ViewModel => (RepackMainViewModel)DataContext;

        public RepackTab()
        {
            InitializeComponent();
        }

        private void Root_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void Root_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                return;
            }

            string[]? items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (items is null || items.Length == 0)
            {
                e.Handled = true;
                return;
            }

            // 본편/업데이트/DLC는 이제 Kind 기준으로 자동 분류되니, 드롭된 항목을 전부 순서대로 추가한다.
            foreach (var item in items)
            {
                if (Directory.Exists(item))
                {
                    ViewModel.AddFolder(item);
                    continue;
                }

                string extension = Path.GetExtension(item);
                if (string.Equals(extension, ".wud", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".wux", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".wua", StringComparison.OrdinalIgnoreCase))
                {
                    await ViewModel.AddFileAsync(item);
                }
            }

            e.Handled = true;
        }

        private void LvFiles_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete)
                return;

            var selected = ViewModel?.SelectedDlcEntry;

            if (selected is not null)
                ViewModel?.DlcEntries.Remove(selected);
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

        private async void BtnRebuild_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsLocked)
            {
                ViewModel.Cancel();
                return;
            }

            await ViewModel.StartAsync(BuildMode.RebuildOnly);
        }
    }
}