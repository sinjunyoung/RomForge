using Common.Localization;
using Microsoft.Win32;
using RomForge.ViewModels.Util;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.Util;

public partial class CertsTab : UserControl
{
    private CertsMainViewModel ViewModel => (CertsMainViewModel)DataContext;

    public CertsTab() => InitializeComponent();

    private void CiaDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        string? cia = files.FirstOrDefault(f => string.Equals(Path.GetExtension(f), ".cia", StringComparison.OrdinalIgnoreCase));

        if (cia != null)
            ViewModel.SetFile(cia);
    }

    private void CiaDrop_Click(object sender, MouseButtonEventArgs e) => BtnAddFile_Click(sender, e);

    private void BtnAddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = CertsMainViewModel.FileDialogFilter,
            Title = Loc.S.DialogSelectCiaFile
        };

        if (dlg.ShowDialog() == true)
            ViewModel.SetFile(dlg.FileName);
    }
}