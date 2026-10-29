using NSW.Core;
using NSW.Core.Models;
using RomForge.Core;
using RomForge.Core.Models.Switch;
using RomForge.Core.ViewModels.Switch;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Res = NSW.Properties.Resources;

namespace RomForge.Controls.Switch;

public partial class CommonListControl : SwitchListControlBase
{
    private readonly ListViewColumnSorter _sorter = new();

    public Button ExtraButton1 => btnExtra1;

    public Action? ExtraButton1Clicked;

    public Button ExtraButton2 => btnExtra2;

    public Action? ExtraButton2Clicked;

    public ObservableCollection<GameFile> GameFiles { get; set; } = [];

    public event Action? FileListChanged;

    protected override ListView? FileListView => lvFiles;

    protected override string? SelectedFilePath => (lvFiles.SelectedItem as GameFile)?.FilePath;

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

    private void UpdateDropHint()
    {
        dropHint.Visibility = GameFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        FileListChanged?.Invoke();
    }

    protected override void RemoveSelectedItems()
    {
        foreach (var item in GetSelectedItems<GameFile>())
            GameFiles.Remove(item);

        UpdateDropHint();
    }

    protected override void RemoveAllItems()
    {
        GameFiles.Clear();
        UpdateDropHint();
    }

    private void BtnExtra1_Click(object sender, RoutedEventArgs e) => ExtraButton1Clicked?.Invoke();

    private void BtnExtra2_Click(object sender, RoutedEventArgs e) => ExtraButton2Clicked?.Invoke();

    protected override async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var candidates = await Task.Run(() => paths.Where(p => SwitchFileTypes.Extensions.Contains(Path.GetExtension(p))).ToList());
        var added = new List<GameFile>();

        foreach (var path in candidates)
        {
            if (GameFiles.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var vm = new GameFile(path)
            {
                FileType = keySet == null ? Res.Status_NoKey : Res.Status_Analyzing,
                TitleName = Path.GetFileNameWithoutExtension(path)
            };

            GameFiles.Add(vm);
            added.Add(vm);
        }

        UpdateDropHint();

        if (keySet == null)
            return;

        foreach (var vm in added)
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

    private static void ApplyInfo(GameFile vm, GameFileInfo? info)
    {
        if (info == null)
        {
            vm.FileType = "?";
            return;
        }

        if (!string.IsNullOrEmpty(info.TitleName))
            vm.TitleName = info.TitleName;

        if (info.IconData != null)
            vm.Icon = info.IconData.ToBitmapImage();

        vm.Naming = info.Naming;
        vm.TitleID = info.TitleId;
        vm.Version = info.DisplayVersion;
        vm.FileType = info.Type;
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) => _sorter.HandleHeaderClick(e, lvFiles);
}