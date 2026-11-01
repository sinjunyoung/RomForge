using NSW.Core;
using NSW.Core.Models;
using RomForge.Core;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls.Switch;

public partial class CommonListControl : CommonListControlBase
{
    private readonly ListViewColumnSorter _sorter = new();

    public Button ExtraButton1 => btnExtra1;

    public Action? ExtraButton1Clicked;

    public Button ExtraButton2 => btnExtra2;

    public Action? ExtraButton2Clicked;

    protected override ListView? TargetListView => lvFiles;

    protected override FrameworkElement? DropHintElement => dropHint;

    public CommonListControl()
    {
        InitializeComponent();

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
        }
        finally
        {
            onCompleted();
        }
    }

    private void BtnExtra1_Click(object sender, RoutedEventArgs e) => ExtraButton1Clicked?.Invoke();

    private void BtnExtra2_Click(object sender, RoutedEventArgs e) => ExtraButton2Clicked?.Invoke();

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) => _sorter.HandleHeaderClick(e, lvFiles);
}