using RomForge.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls.Settings;

public partial class KeyFileSettingsTab : UserControl
{
    public KeyFileSettingsTab() => InitializeComponent();

    private void KeyPathTextBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void KeyPathTextBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && sender is FrameworkElement { DataContext: KeyFilePathItemViewModel item })
            item.FilePath = files[0];

        e.Handled = true;
    }
}