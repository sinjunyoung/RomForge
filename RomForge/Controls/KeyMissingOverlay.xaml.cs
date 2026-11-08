using RomForge.Core.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls;

public partial class KeyMissingOverlay : UserControl
{
    public KeyMissingOverlay() => InitializeComponent();

    private void Overlay_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Overlay_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && DataContext is KeyOverlayViewModel vm)
            vm.Accept(files[0]);

        e.Handled = true;
    }
}