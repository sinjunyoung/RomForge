using Common.Localization;
using Microsoft.Win32;
using RomForge.Core.Models.N3DS;
using RomForge.ViewModels.N3DS;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RomForge.Controls.N3DS;

public partial class InstalledTitlesTab : UserControl
{
    private InstallerMainViewModel Vm => (InstallerMainViewModel)DataContext;

    public InstalledTitlesTab()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is InstallerMainViewModel vm)
                TitleListView.ItemsSource = vm.InstalledTitles.FilteredTitles;
        };
    }

    private async void LoadTitles_Click(object sender, RoutedEventArgs e)
    {
        await Vm.LoadTitlesAsync((current, total) => Dispatcher.Invoke(() =>
        {
            LoadingProgress.Maximum = total;
            LoadingProgress.Value = current;
            LoadingText.Text = Loc.S.ProgressLoadingCount(current, total);
        }));
    }

    private async void RecoverTitleDb_Click(object sender, RoutedEventArgs e) => await Vm.RecoverTitleDbAsync();

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is InstallerMainViewModel vm)
            vm.InstalledTitles.RefreshFilter(SearchBox.Text);
    }

    private ToggleButton[] FilterButtons => [FilterAll, FilterGame, FilterUpdate, FilterDlc];

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn)
            return;

        foreach (var b in FilterButtons)
            b.IsChecked = b == btn;

        Vm.InstalledTitles.SetFilter(btn.Tag?.ToString() ?? "All", SearchBox.Text);
    }

    private void ContextMenu_Opening(object sender, ContextMenuEventArgs e)
    {
        if (Vm.InstalledTitles.IsLocked)
        {
            e.Handled = true;
            return;
        }

        var selected = TitleListView.SelectedItems.Cast<TitleViewModel>().ToList();
        bool allBase = selected.All(t => t.Title.IsApplication);

        MenuExtractCci.IsEnabled = allBase;
        MenuExtractCci.Visibility = allBase ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ExtractTitle_Click(object sender, RoutedEventArgs e)
    {
        if (TitleListView.SelectedItem is not TitleViewModel selected)
            return;

        bool asCci = sender == MenuExtractCci;
        var (ext, desc) = asCci ? ("cci", "CCI(CTR Cartridge Image)") : ("cia", "CIA(CTR Importable Archive)");
        var dialog = new SaveFileDialog
        {
            Title = Loc.S.DialogSelectSaveLocation(desc),
            Filter = $"{Loc.S.FilterFilesOfType(desc)}|*.{ext}",
            FileName = $"{selected.ShortDescription}.{ext}"
        };

        if (dialog.ShowDialog() != true)
            return;

        await Vm.ExtractTitleAsync(selected, dialog.FileName, asCci);
    }
}