using RomForge.Core.Services.Wii;
using RomForge.ViewModels.Wii;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls.Wii;

public partial class MainTab : UserControl
{
    private static readonly HashSet<string> DiscExtensions = new(StringComparer.OrdinalIgnoreCase) { ".iso", ".wbfs", ".rvz", ".wia" };

    private WiiMainViewModel ViewModel => (WiiMainViewModel)DataContext;

    public MainTab()
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
        var items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var path = items?.FirstOrDefault(p => File.Exists(p) && DiscExtensions.Contains(Path.GetExtension(p)));

        if (path != null)
            ViewModel.InputPath = path;

        e.Handled = true;
    }

    private void TxtPatch_Drop(object sender, DragEventArgs e)
    {
        var items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var path = items?.FirstOrDefault(RiivolutionWorkspace.IsSupported);

        if (path != null)
            ViewModel.PatchPath = path;

        e.Handled = true;
    }

    private void TxtPatch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        txtPatch.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

        e.Handled = true;
    }

    private async void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
        {
            ViewModel.Cancel();
            return;
        }

        await ViewModel.RunAsync();
    }
}