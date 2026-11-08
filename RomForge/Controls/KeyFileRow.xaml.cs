using RomForge.Core.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls;

public partial class KeyFileRow : UserControl
{
    public KeyFileRow() => InitializeComponent();

    private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void TextBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && DataContext is KeyFilePathItemViewModel item)
            item.Accept(files[0]);

        e.Handled = true;
    }
}