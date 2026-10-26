using RomForge.Core.ViewModels.Switch;
using RomForge.ViewModels.Switch;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls.Switch;

public partial class SplitListControl : SwitchListControlBase
{
    private SplitMainViewModel ViewModel => (SplitMainViewModel)DataContext;

    public SplitListControl() => InitializeComponent();

    protected override ListView? FileListView => lvFiles;

    protected override string? SelectedFilePath => (lvFiles.SelectedItem as SplitRowViewModel)?.FilePath;

    protected override Task AddPathsAsync(IEnumerable<string> paths) => ViewModel?.AddFilesAsync(paths) ?? Task.CompletedTask;

    protected override void RemoveSelectedItems() => ViewModel?.RemoveRows(GetSelectedItems<SplitRowViewModel>());

    protected override void RemoveAllItems() => ViewModel?.ClearAll();

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e) => ViewModel?.SelectAll(true);

    private void BtnSelectNone_Click(object sender, RoutedEventArgs e) => ViewModel?.SelectAll(false);

    private void BtnExceptBase_Click(object sender, RoutedEventArgs e) => ViewModel?.SelectExceptBase();

    private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            bool isChecked = checkBox.IsChecked == true;

            ViewModel?.SelectAll(isChecked);
        }
    }

    private void BtnExpand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SplitRowViewModel row })
            ViewModel?.ToggleExpand(row);

        e.Handled = true;
    }
}