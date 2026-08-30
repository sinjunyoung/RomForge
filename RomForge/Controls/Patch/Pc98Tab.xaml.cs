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

    private void PatchDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "한글패치 ZIP 선택", Filter = "ZIP 파일|*.zip|모든 파일|*.*" };

        if (dlg.ShowDialog() == true)
            ViewModel.PatchVM.Pc98VM.PatchPath = dlg.FileName;
    }

    private void PatchDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            string path = files[0];

            ViewModel.PatchVM.Pc98VM.PatchPath = Directory.Exists(path) || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? path
                : Path.GetDirectoryName(path) ?? path;
        }
    }
}
