using Microsoft.Win32;
using RomForge.Core.Models.Patch;
using RomForge.ViewModels;
using RomForge.ViewModels.Patch;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls;

public partial class PatchTab : UserControl
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private static class PatchExtensions
    {
        public static readonly string[] AllowedExtensions = [".ips", ".bps", ".ups", ".ppf", ".aps", ".xdelta"];

        public static string FileFilter => $"패치 파일|{string.Join(";", AllowedExtensions.Select(ext => "*" + ext))}|모든 파일|*.*";
    }

    public PatchTab()
    {
        InitializeComponent();
    }

    private static string? OpenSingleFileDialog(string title)
    {
        var dlg = new OpenFileDialog { Title = title };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private void NormalSourceDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var path = OpenSingleFileDialog("원본 파일 선택");

        if (path != null)
            ViewModel.PatchVM.NormalVM.SourcePath = path;
    }

    private void NormalSourceDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            ViewModel.PatchVM.NormalVM.SourcePath = files[0];
    }

    private void NormalPatchDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog 
        { 
            Title = "패치 파일 선택",
            Filter = PatchExtensions.FileFilter
        };

        if (dlg.ShowDialog() == true)
            ViewModel.PatchVM.NormalVM.PatchPath = dlg.FileName;
    }

    private void NormalPatchDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            string filePath = files[0];
            string extension = Path.GetExtension(filePath).ToLower();

            if (PatchExtensions.AllowedExtensions.Contains(extension))
                ViewModel.PatchVM.NormalVM.PatchPath = filePath;
        }
    }

    private void ArcadeSourceDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var path = OpenSingleFileDialog("원본 ZIP 선택");

        if (path != null)
            ViewModel.PatchVM.ArcadeVM.SourcePath = path;
    }

    private void ArcadeSourceDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            ViewModel.PatchVM.ArcadeVM.SourcePath = files[0];
    }

    private void ArcadePatchDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var folderDlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "패치 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (folderDlg.ShowDialog() == true)
            ViewModel.PatchVM.ArcadeVM.PatchPath = folderDlg.SelectedPath;
    }

    private void ArcadePatchDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        ViewModel.PatchVM.ArcadeVM.PatchPath = files[0];
    }

    private void MatchCard_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void MatchCard_PatchDrop(object sender, DragEventArgs e)
    {
        if (sender is not Border border)
            return;

        if (border.Tag is not ArcadeMatchItem item) 
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        string path = files[0];
        var patchEntry = new PatchEntry
        {
            DisplayName = Path.GetFileName(path),
            EntryPath = path
        };

        ViewModel.PatchVM.ArcadeVM.ManualMatch(item, patchEntry);
    }

    private void PatchPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo)
            return;

        if (combo.Tag is not ArcadeMatchItem item) 
            return;

        if (combo.SelectedItem is not PatchEntry entry) 
            return;

        if (ReferenceEquals(entry, item.PatchEntry)) 
            return;

        ViewModel.PatchVM.ArcadeVM.ManualMatch(item, entry);
    }
}
