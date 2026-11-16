using Common.Localization;
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

    public Pc98Tab() => InitializeComponent();

    private void SourceDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = Loc.S.DialogSelectSourceHdi, Filter = $"{Loc.S.FilterPc98DiskImage}|*.hdi;*.nhd;*.thd|{Loc.S.FilterAllFiles}|*.*" };

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
            Title = Loc.S.DialogSelectPatchFolder
        };

        if (dialog.ShowDialog() == true)
            ViewModel.PatchVM.Pc98VM.PatchPath = dialog.FolderName;
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{Loc.S.FilterPatchFiles} (*.ips;*.zip)|*.ips;*.zip|{Loc.S.FilterFilesOfType("IPS")} (*.ips)|*.ips|{Loc.S.FilterFilesOfType("ZIP")} (*.zip)|*.zip|{Loc.S.FilterAllFiles} (*.*)|*.*",
            Title = Loc.S.DialogSelectPatchFile
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
                MessageBox.Show(Loc.S.ErrOnlyFolderIpsZipRegistrable, Loc.S.MsgTitleError, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}