using NSW.Core;
using NSW.Core.Models;
using RomForge.Converters;
using RomForge.Core;
using RomForge.Core.Services.Switch;
using RomForge.Core.ViewModels.Switch;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RomForge.Controls.Switch;

public partial class GroupedSwitchListControl : CommonListControlBase
{
    private readonly ListViewColumnSorter _sorter = new();

    public Button ExtraButton1 => btnExtra1;

    public Action? ExtraButton1Clicked;

    public Button ExtraButton2 => btnExtra2;

    public Action? ExtraButton2Clicked;

    protected override ListView? TargetListView => lvFiles;

    protected override FrameworkElement? DropHintElement => dropHint;

    public GroupedSwitchListControl()
    {
        InitializeComponent();

        if (CollectionViewSource.GetDefaultView(GameFiles) is ListCollectionView view)
        {
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GameFile.TitleID), new TitleIdGroupConverter()));

            view.CustomSort = new SwitchItemSortComparer();
        }

        lvFiles.ItemsSource = GameFiles;

        UpdateDropHint();
    }

    public static bool KeyExists() => KeySetProvider.Instance.KeySet != null;

    public void RecalcKeyMissingFiles(Action onCompleted) => _ = RecalcKeyMissingFilesAsync(onCompleted);

    private async Task RecalcKeyMissingFilesAsync(Action onCompleted)
    {
        try
        {
            var keySet = KeySetProvider.Instance.KeySet;

            if (keySet == null)
                return;

            var targets = GameFiles.Where(f => f.IsKeyMissing).ToList();

            foreach (var vm in targets)
            {
                var info = await Task.Run<GameFileInfo?>(() =>
                {
                    try
                    {
                        return MetadataReader.ReadGameFileInfo(keySet.Clone(), vm.FilePath);
                    }
                    catch
                    {
                        return null;
                    }
                });

                ApplyInfo(vm, info);
            }

            CollectionViewSource.GetDefaultView(GameFiles)?.Refresh();
        }
        finally
        {
            onCompleted();
        }
    }

    protected override async Task AddPathsAsync(IEnumerable<string> paths)
    {
        await base.AddPathsAsync(paths);
        CollectionViewSource.GetDefaultView(GameFiles)?.Refresh();
    }

    protected override void RemoveSelectedItems()
    {
        base.RemoveSelectedItems();
        CollectionViewSource.GetDefaultView(GameFiles)?.Refresh();
    }

    protected override void RemoveAllItems()
    {
        base.RemoveAllItems();
        CollectionViewSource.GetDefaultView(GameFiles)?.Refresh();
    }

    private void BtnExtra1_Click(object sender, RoutedEventArgs e) => ExtraButton1Clicked?.Invoke();

    private void BtnExtra2_Click(object sender, RoutedEventArgs e) => ExtraButton2Clicked?.Invoke();

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) => _sorter.HandleHeaderClick(e, lvFiles);
}