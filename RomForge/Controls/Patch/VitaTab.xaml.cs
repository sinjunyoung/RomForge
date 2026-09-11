using LibHac.Ncm;
using Microsoft.Win32;
using RomForge.ViewModels;
using RomForge.ViewModels.Patch;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.Patch;

public partial class VitaTab : UserControl
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public VitaTab()
    {
        InitializeComponent();
    }

    private void SourceDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "원본 폴더 선택 (app/patch/addcont가 있는 폴더)",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            ViewModel.PatchVM.VitaVM.SourcePath = dlg.SelectedPath;
    }

    private void SourceDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            ViewModel.PatchVM.VitaVM.SourcePath = files[0];
    }

    private void PatchDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "xdelta 패치 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
        {
            ViewModel.PatchVM.VitaVM.PatchPath = dlg.SelectedPath;
            return;
        }

        var fileDlg = new OpenFileDialog
        {
            Title = "xdelta 패치 ZIP 선택",
            Filter = "ZIP 파일|*.zip|모든 파일|*.*"
        };

        if (fileDlg.ShowDialog() == true)
            ViewModel.PatchVM.VitaVM.PatchPath = fileDlg.FileName;
    }

    private void PatchDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            ViewModel.PatchVM.VitaVM.PatchPath = files[0];
    }

    private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "출력 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            ViewModel.PatchVM.VitaVM.OutputPath = dlg.SelectedPath;
    }
}