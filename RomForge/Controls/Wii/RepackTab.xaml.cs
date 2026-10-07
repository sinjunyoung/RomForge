using NSW.Core.Enums;
using RomForge.Core.Services.Wii;
using RomForge.ViewModels.Wii;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.Wii;

public partial class RepackTab : UserControl
{
    private RepackMainViewModel ViewModel => (RepackMainViewModel)DataContext;

    public RepackTab()
    {
        InitializeComponent();
    }

    private void Drag_Over(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void TxtRom_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (!ViewModel.IsIdle)
            return;

        var items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var path = items?.FirstOrDefault(RepackMainViewModel.IsSupportedDisc);

        if (path != null)
            ViewModel.InputPath = path;
    }

    private void TxtPatch_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (!ViewModel.IsIdle)
            return;

        var items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var path = items?.FirstOrDefault(RiivolutionWorkspace.IsSupported);

        if (path != null)
            ViewModel.PatchPath = path;
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

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
        {
            ViewModel.Cancel();
            return;
        }

        await ViewModel.StartAsync(BuildMode.FullProcess);
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://sinjunyoung.github.io/RomForge/wii-merge/",
            UseShellExecute = true
        };

        System.Diagnostics.Process.Start(psi);
    }
}