using Microsoft.Win32;
using NSW.Core;
using NSW.WPF.Services;
using NSW.WPF.ViewModels;
using Patch.Core.Services;
using RomForge.Views;
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

    private readonly GameFilePatchSyncManager _patchSync;
    private readonly SwitchGameFileImporter _importer;

    public SwitchTitleListControl()
    {
        InitializeComponent();
        lvFiles.ItemsSource = GameFiles;
        _patchSync = new GameFilePatchSyncManager(GameFiles);
        _importer = new SwitchGameFileImporter(GameFiles, _patchSync, SupportedExtensions);
        UpdateDropHint();
    }

    public static bool KeyExists() => KeySetProvider.Instance.KeySet != null;

    public void RecalcKeyMissingFiles(Action onCompleted)
    {
        var targets = GameFiles.Where(f => f.IsKeyMissing).ToList();

        if (targets.Count == 0)
        {
            onCompleted();
            return;
        }

        var keySet = KeySetProvider.Instance.KeySet;

        if (keySet == null)
        {
            onCompleted();
            return;
        }

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
            _ = _importer.AddFilesAsync(SwitchPatchDropValidator.ExpandPaths(dlg.FileNames), UpdateDropHint);
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "게임 폴더 선택", UseDescriptionForTitle = true };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _ = _importer.AddFilesAsync(SwitchPatchDropValidator.ExpandPaths([dlg.SelectedPath]), UpdateDropHint);
    }

    private void BtnBulkPatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: not null } fe)
            return;

        fe.ContextMenu.PlacementTarget = fe;
        fe.ContextMenu.IsOpen = true;
    }

    private void BulkPatchMenu_FromFolder_Click(object sender, RoutedEventArgs e)
    {
        var targets = GetBulkPatchTargets();

        if (targets == null) 
            return;

        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "한글패치 루트 폴더 선택 (titleId 이름의 폴더 또는 titleId.zip/titleId.7z 파일을 자동 매칭합니다)",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        int matched = SwitchBulkPatchMatcher.MatchFromFolder(targets, dlg.SelectedPath);

        ShowBulkPatchResult(targets.Count, matched);
    }

    private async void BulkPatchMenu_FromArchive_Click(object sender, RoutedEventArgs e)
    {
        var targets = GetBulkPatchTargets();

        if (targets == null)
            return;

        var dlg = new OpenFileDialog
        {
            Title = "한글패치 루트 압축파일 선택 (안에서 titleId 이름의 폴더를 자동 매칭합니다)",
            Filter = "압축파일 (*.zip;*.7z)|*.zip;*.7z"
        };

        if (dlg.ShowDialog() != true)
            return;

        int matched;

        try
        {
            var opened = await PasswordPromptWindow.OpenWithPasswordPromptAsync(dlg.FileName, Window.GetWindow(this));

            if (opened == null)
                return;

            using var archive = opened.Value.Archive;

            matched = SwitchBulkPatchMatcher.MatchFromArchive(targets, archive, dlg.FileName, opened.Value.Password);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"압축파일을 여는 중 오류가 발생했습니다: {ex.Message}", "한글패치 일괄 지정", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        ShowBulkPatchResult(targets.Count, matched);
    }

    private List<GameFile>? GetBulkPatchTargets()
    {
        var targets = GameFiles.Where(f => !string.IsNullOrEmpty(f.TitleID)).ToList();

        if (targets.Count == 0)
        {
            MessageBox.Show("타이틀 정보가 있는 항목이 없습니다.", "한글패치 일괄 지정", MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        return targets;
    }

    private static void ShowBulkPatchResult(int total, int matched) =>
        MessageBox.Show($"{total}개 중 {matched}개에 패치 매칭됨.", "한글패치 일괄 지정", MessageBoxButton.OK, MessageBoxImage.Information);

    private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in lvFiles.SelectedItems.Cast<GameFile>().ToList())
        {
            _patchSync.Detach(item);
            GameFiles.Remove(item);
        }

        SwitchGameFileListOrganizer.Reorganize(GameFiles);
        UpdateDropHint();
    }

    private void BtnRemoveAllFiles_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in GameFiles)
            _patchSync.Detach(item);

        GameFiles.Clear();
        UpdateDropHint();
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://sinjunyoung.github.io/RomForge/switch-unpack-repack/",
            UseShellExecute = true
        };

        System.Diagnostics.Process.Start(psi);
    }

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

        await _importer.AddFilesAsync(SwitchPatchDropValidator.ExpandPaths(paths), UpdateDropHint);
    }

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = lvFiles.SelectedIndex;

        if (selected < 0)
            return;

        string? dir = Path.GetDirectoryName(GameFiles[selected].FilePath);

        dir?.OpenFolder();
    }

    private void MenuItem_RemovePatch_Click(object sender, RoutedEventArgs e)
    {
        if (lvFiles.SelectedItem is GameFile file)
            _patchSync.ClearPatch(file);
    }

    private void PatchDropTarget_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: not null } fe)
            return;

        fe.ContextMenu.PlacementTarget = fe;
        fe.ContextMenu.IsOpen = true;
    }

    private void PatchMenu_SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GameFile file })
            return;

        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = $"{file.TitleName}에 적용할 한글패치 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            file.PatchPath = dlg.SelectedPath;
    }

    private async void PatchMenu_SelectArchive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GameFile file })
            return;

        var dlg = new OpenFileDialog
        {
            Title = $"{file.TitleName}에 적용할 한글패치 압축파일 선택",
            Filter = "압축파일 (*.zip;*.7z)|*.zip;*.7z"
        };

        if (dlg.ShowDialog() != true)
            return;

        if (!await TryAssignArchivePatchAsync(file, dlg.FileName))
            return;
    }

    private void PatchDropTarget_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = SwitchPatchDropValidator.IsValidPatchDrop(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void PatchDropTarget_DragLeave(object sender, DragEventArgs e) => e.Handled = true;

    private async void PatchDropTarget_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GameFile file })
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;

        string path = paths[0];

        if (!SwitchPatchDropValidator.IsValidPatchPath(path))
            return;

        if (ArchivePatchSourceFactory.IsArchivePath(path))
            await TryAssignArchivePatchAsync(file, path);
        else
            file.PatchPath = path;
    }

    private async Task<bool> TryAssignArchivePatchAsync(GameFile file, string archivePath)
    {
        try
        {
            var opened = await PasswordPromptWindow.OpenWithPasswordPromptAsync(archivePath, Window.GetWindow(this));

            if (opened == null)
                return false;

            opened.Value.Archive.Dispose();

            file.PatchPath = archivePath;
            file.PatchPassword = opened.Value.Password;

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"압축파일을 여는 중 오류가 발생했습니다: {ex.Message}", "한글패치 지정", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}