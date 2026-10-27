using Common;
using Microsoft.Win32;
using NSW.Core;
using Patch.Core.Services;
using RomForge.Core;
using RomForge.Core.Models.Switch;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using RomForge.Core.ViewModels.Switch;
using RomForge.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PatchSource = RomForge.Core.Services.Switch.PatchSource;

namespace RomForge.Controls.Switch;

public partial class RepackListControl : SwitchListControlBase
{
    private readonly RepackRowViewModel _viewModel;

    public ObservableCollection<GameFile> GameFiles => _viewModel.GameFiles;

    public event Action? FileListChanged
    {
        add => _viewModel.FileListChanged += value;
        remove => _viewModel.FileListChanged -= value;
    }

    public RepackListControl()
    {
        InitializeComponent();

        _viewModel = new RepackRowViewModel(SwitchFileTypes.Extensions);
        DataContext = _viewModel;
        lvFiles.ItemsSource = _viewModel.GameFiles;
        _viewModel.FileListChanged += UpdateDropHint;

        UpdateDropHint();
    }

    public static bool KeyExists() => KeySetProvider.Instance.KeySet != null;

    public void RecalcKeyMissingFiles(Action onCompleted) => _viewModel.RecalcKeyMissingFiles(onCompleted);

    private void UpdateDropHint() => dropHint.Visibility = _viewModel.GameFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    protected override ListView? FileListView => lvFiles;

    protected override string? SelectedFilePath => (lvFiles.SelectedItem as GameFile)?.FilePath;

    protected override Task AddPathsAsync(IEnumerable<string> paths) => _viewModel.AddFilesAsync(paths);

    protected override void RemoveSelectedItems()
    {
        foreach (var item in GetSelectedItems<GameFile>())
            _viewModel.RemoveFile(item);
    }

    protected override void RemoveAllItems() => _viewModel.RemoveAllFiles();

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

        var report = BulkPatchMatcher.MatchFromFolder(targets, dlg.SelectedPath, _viewModel.GetExpectedIds);

        _viewModel.ProcessBulkPatchResult(report);
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

        try
        {
            var opened = await PasswordPromptWindow.OpenWithPasswordPromptAsync(dlg.FileName, Window.GetWindow(this));

            if (opened == null)
                return;

            using var archive = opened.Value.Archive;
            var report = BulkPatchMatcher.MatchFromArchive(targets, archive, dlg.FileName, opened.Value.Password, _viewModel.GetExpectedIds);

            _viewModel.ProcessBulkPatchResult(report);
        }
        catch (Exception ex)
        {
            _viewModel.AppendLog($"압축파일을 여는 중 오류가 발생했습니다: {ex.Message}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
        }
    }

    private List<GameFile>? GetBulkPatchTargets()
    {
        var targets = _viewModel.GameFiles.Where(f => !string.IsNullOrEmpty(f.TitleID)).ToList();

        if (targets.Count == 0)
        {
            _viewModel.AppendLog("타이틀 정보가 있는 항목이 없습니다.", LogLevel.Info);
            return null;
        }

        return targets;
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

    private void MenuItem_RemovePatch_Click(object sender, RoutedEventArgs e)
    {
        if (lvFiles.SelectedItem is GameFile file)
            _viewModel.ClearPatch(file);
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
            _viewModel.TryAssignPatch(file, PatchSource.FromFolder(dlg.SelectedPath), null);
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

        await TryAssignArchivePatchAsync(file, dlg.FileName);
    }

    private void PatchDropTarget_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = PatchDropValidator.IsValidPatchDrop(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
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

        if (!PatchDropValidator.IsValidPatchPath(path))
            return;

        if (ArchivePatchSourceFactory.IsArchivePath(path))
            await TryAssignArchivePatchAsync(file, path);
        else
            _viewModel.TryAssignPatch(file, PatchSource.FromFolder(path), null);
    }

    private async Task<bool> TryAssignArchivePatchAsync(GameFile file, string archivePath)
    {
        try
        {
            var opened = await PasswordPromptWindow.OpenWithPasswordPromptAsync(archivePath, Window.GetWindow(this));

            if (opened == null)
                return false;

            var source = PatchSource.FromArchive(archivePath, opened.Value.Archive);

            opened.Value.Archive.Dispose();

            return _viewModel.TryAssignPatch(file, source, opened.Value.Password);
        }
        catch (Exception ex)
        {
            _viewModel.AppendLog($"압축파일을 여는 중 오류가 발생했습니다: {ex.Message}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return false;
        }
    }
}