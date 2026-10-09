using Microsoft.Win32;
using RomForge.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.Patch;

public partial class Pc98Tab : UserControl
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public Pc98Tab()
    {
        InitializeComponent();
    }

    private void SourceDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "원본 HDI 선택", Filter = "PC98 디스크 이미지|*.hdi;*.nhd;*.thd|모든 파일|*.*" };

        if (dlg.ShowDialog() == true)
            ViewModel.PatchVM.Pc98VM.SourcePath = dlg.FileName;
    }

    private void SourceDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            ViewModel.PatchVM.Pc98VM.SourcePath = files[0];
    }

    private void PatchDrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
            ViewModel.PatchVM.Pc98VM.PatchPath = dialog.FolderName;
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "패치 파일 (*.ips;*.zip)|*.ips;*.zip|IPS 파일 (*.ips)|*.ips|ZIP 압축 파일 (*.zip)|*.zip|모든 파일 (*.*)|*.*",
            Title = "패치 파일 선택"
        };

        if (dialog.ShowDialog() == true)
            ViewModel.PatchVM.Pc98VM.PatchPath = dialog.FileName;
    }

    private void PatchDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            string path = files[0];
            string ext = Path.GetExtension(path);

            if (Directory.Exists(path) || ext.Equals(".ips", StringComparison.OrdinalIgnoreCase) || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                ViewModel.PatchVM.Pc98VM.PatchPath = path;
            else
                MessageBox.Show("폴더 또는 IPS/ZIP 파일만 등록할 수 있습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}