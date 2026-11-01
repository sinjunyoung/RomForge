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

public class CommonListControlBase : SwitchListControlBase
{
    protected virtual ListView? TargetListView => null;

    protected virtual FrameworkElement? DropHintElement => null;

    public ObservableCollection<GameFile> GameFiles { get; set; } = [];

    public event Action? FileListChanged;

    protected override ListView? FileListView => TargetListView;

    protected override string? SelectedFilePath => (TargetListView?.SelectedItem as GameFile)?.FilePath;

    protected void UpdateDropHint()
    {
        DropHintElement?.Visibility = GameFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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

    protected static void ApplyInfo(GameFile vm, GameFileInfo? info)
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
}