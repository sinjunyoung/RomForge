using Common;
using Common.WPF;
using Microsoft.Win32;
using NSW.Core;
using NSW.Core.Models;
using NSW.Core.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Res = NSW.Properties.Resources;

namespace RomForge.Controls.Switch;

public partial class CommonListControl : UserControl
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".nsp", ".xci", ".nsz", ".xcz" };
    private readonly ListViewColumnSorter _sorter = new();

    public Button ExtraButton1 => btnExtra1;

    public Action ExtraButton1Clicked;

    public Button ExtraButton2 => btnExtra2;

    public Action ExtraButton2Clicked;

    public ObservableCollection<GameFile> GameFiles { get; set; } = [];

    public event Action? FileListChanged;

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

    private void BtnAddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = Res.Dialog_SelectGameFile,
            Filter = $"{Res.Filter_SwitchFiles} (*.nsp;*.xci;*.nsz;*.xcz)|*.nsp;*.xci;*.nsz;*.xcz|{Res.Filter_AllFiles}|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog() == true)
            _ = AddFilesAsync(Common.Utils.ExpandPaths(dlg.FileNames));
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "게임 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _ = AddFilesAsync(Common.Utils.ExpandPaths([dlg.SelectedPath]));
    }

    private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in lvFiles.SelectedItems.Cast<GameFile>().ToList())
            GameFiles.Remove(item);

        UpdateDropHint();
    }

    private void BtnRemoveAllFiles_Click(object sender, RoutedEventArgs e)
    {
        GameFiles.Clear();
        UpdateDropHint();
    }

    private void BtnExtra1_Click(object sender, RoutedEventArgs e) => ExtraButton1Clicked?.Invoke();

    private void BtnExtra2_Click(object sender, RoutedEventArgs e) => ExtraButton2Clicked?.Invoke();

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            BtnRemoveFile_Click(sender, new RoutedEventArgs());
    }

    private void LvFiles_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        await AddFilesAsync(Common.Utils.ExpandPaths(paths));
    }

    private async Task AddFilesAsync(IEnumerable<string> paths)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var candidates = await Task.Run(() => paths.Where(p => SupportedExtensions.Contains(Path.GetExtension(p))).ToList());
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

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (lvFiles.SelectedItem is not GameFile selected)
            return;

        Path.GetDirectoryName(selected.FilePath)?.OpenFolder();
    }
}