using Microsoft.Win32;
using NSW.Core;
using NSW.WPF.Services;
using NSW.WPF.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.Controls.Switch;

public partial class SwitchTitleListControl : UserControl
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".nsp", ".xci", ".nsz", ".xcz" };

    public ObservableCollection<GameFile> GameFiles { get; set; } = [];

    public event Action? FileListChanged;

    public SwitchTitleListControl()
    {
        InitializeComponent();
        lvFiles.ItemsSource = GameFiles;
        UpdateDropHint();
    }

    public static bool KeyExists() => KeySetProvider.Instance.KeySet != null;

    public void RecalcKeyMissingFiles(Action onCompleted)
    {
        var targets = GameFiles.Where(f => f.IsKeyMissing).ToList();
        if (targets.Count == 0) { onCompleted(); return; }

        var keySet = KeySetProvider.Instance.KeySet;
        if (keySet == null) { onCompleted(); return; }

        int remaining = targets.Count;
        foreach (var vm in targets)
        {
            string capturedPath = vm.FilePath;
            _ = Task.Run(() =>
            {
                string result = MetadataReader.DetectFileType(keySet, capturedPath);

                if (Interlocked.Decrement(ref remaining) == 0)
                    Dispatcher.Invoke(() => { vm.FileType = result; onCompleted(); });
                else
                    Dispatcher.Invoke(() => vm.FileType = result);
            });
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
            _ = AddFilesAsync(ExpandPaths(dlg.FileNames));
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "게임 폴더 선택", UseDescriptionForTitle = true };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _ = AddFilesAsync(ExpandPaths([dlg.SelectedPath]));
    }

    private void BtnBulkPatch_Click(object sender, RoutedEventArgs e)
    {
        var dlcEntries = GameFiles.Where(f => f.FileType.Contains('D')).ToList();

        if (dlcEntries.Count == 0)
        {
            MessageBox.Show("DLC 항목이 없습니다.", "DLC 패치 일괄 지정", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "DLC 패치 루트 폴더 선택 (titleId 이름의 하위 폴더를 자동 매칭합니다)",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        int matched = 0;

        foreach (var dlc in dlcEntries)
        {
            if (string.IsNullOrEmpty(dlc.TitleID))
                continue;

            string candidate = Path.Combine(dlg.SelectedPath, dlc.TitleID);

            if (Directory.Exists(candidate))
            {
                dlc.PatchPath = candidate;
                matched++;
            }
        }

        MessageBox.Show($"DLC {dlcEntries.Count}개 중 {matched}개에 패치 매칭됨.", "DLC 패치 일괄 지정", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void LvFiles_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) BtnRemoveFile_Click(sender, new RoutedEventArgs());
    }

    private void LvFiles_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        await AddFilesAsync(ExpandPaths(paths));
    }

    private async Task AddFilesAsync(IEnumerable<string> paths)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var existing = GameFiles.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newPaths = await Task.Run(() =>
            paths.Where(p => SupportedExtensions.Contains(Path.GetExtension(p)))
                 .Where(p => existing.Add(p))
                 .ToList());

        foreach (var path in newPaths)
        {
            var vm = new GameFile(path) { FileType = keySet == null ? Res.Status_NoKey : Res.Status_Analyzing };

            if (keySet != null)
            {
                var info = MetadataReader.GetGameFileInfo(keySet, path);
                if (info != null)
                {
                    vm.TitleName = info.TitleName;
                    vm.TitleID = info.TitleId;
                    vm.Version = info.DisplayVersion;
                    vm.FileType = info.Type;
                    if (info.IconData != null) vm.Icon = info.IconData.ToBitmapImage();
                }
            }

            if (string.IsNullOrEmpty(vm.TitleName))
                vm.TitleName = Path.GetFileNameWithoutExtension(path);

            AssignOrReplace(vm);
            UpdateDropHint();
        }
    }

    // 본편(B)/업데이트(U)는 새로 들어오면 기존 걸 대체, DLC(D)나 미확인 항목은 그냥 누적.
    private void AssignOrReplace(GameFile vm)
    {
        if (vm.FileType.Contains('B'))
        {
            var existingBase = GameFiles.FirstOrDefault(f => f.FileType.Contains('B'));
            if (existingBase != null)
            {
                vm.PatchPath ??= existingBase.PatchPath;
                GameFiles.Remove(existingBase);
            }
        }

        if (vm.FileType.Contains('U'))
        {
            var existingUpdate = GameFiles.FirstOrDefault(f => f.FileType.Contains('U'));
            if (existingUpdate != null)
            {
                vm.PatchPath ??= existingUpdate.PatchPath;
                GameFiles.Remove(existingUpdate);
            }
        }

        GameFiles.Add(vm);
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        var opts = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = FileAttributes.System | FileAttributes.Hidden };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*.*", opts)) yield return f;
            else if (File.Exists(path))
                yield return path;
        }
    }

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0) e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedIndex;
        if (selected < 0) return;

        string? dir = Path.GetDirectoryName(GameFiles[selected].FilePath);
        dir?.OpenFolder();
    }

    private void MenuItem_RemovePatch_Click(object sender, RoutedEventArgs e)
    {
        if (lvFiles.SelectedItem is GameFile file)
            file.PatchPath = null;
    }

    private void BtnSetPatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GameFile file }) return;

        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = $"{file.TitleName}에 적용할 한글패치 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            file.PatchPath = dlg.SelectedPath;
    }
}