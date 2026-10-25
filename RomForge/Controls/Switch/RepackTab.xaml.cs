using Common;
using RomForge.ViewModels.Switch;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Path = System.IO.Path;

namespace RomForge.Controls.Switch;

public partial class RepackTab : UserControl
{
    RepackMainViewModel ViewModel => (RepackMainViewModel)DataContext;

    private CancellationTokenSource? _cts;

    public RepackTab()
    {
        InitializeComponent();
        Loaded += RepackTab_Loaded;
    }

    private void RepackTab_Loaded(object sender, RoutedEventArgs e)
    {
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            return;

        if (titleList.DataContext is TitleListViewModel vm)
            vm.LogAction = (msg, level) => ViewModel.AppendLog(msg, level);

        titleList.FileListChanged += SyncAll;

        SyncAll();
    }

    private async void SyncAll()
    {
        _cts?.Cancel();

        _cts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, _cts.Token);
            await langTab.UpdateLanguageTabAsync(titleList.GameFiles, Path.Combine(ViewModel.OutputPath, "unpacked"));
            SyncContext();
        }
        catch (OperationCanceledException) { }
    }

    private void SyncContext() => ViewModel.Context = new RepackMainViewModel.BuildContext(titleList.GameFiles, langTab.CurrentMetadata, langTab.ForcedLanguage, langTab.TargetIdOffset, langTab.OverrideTitleId);

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked) 
        { 
            ViewModel.Cancel();
            return;
        }

        await RecalcIfNeededAsync();
        langTab.SyncMetadataFromUI();
        SyncContext();

        await ViewModel.StartAsync(BuildMode.FullProcess);
    }

    private async void BtnUnpack_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked) 
        { 
            ViewModel.Cancel(); 
            return; 
        }

        await RecalcIfNeededAsync();
        SyncContext();

        await ViewModel.StartAsync(BuildMode.UnpackOnly);
    }

    private void BtnRebuild_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked) 
        { 
            ViewModel.Cancel(); 
            return;
        }

        SyncContext();
        _ = ViewModel.StartAsync(BuildMode.RebuildOnly);
    }

    private async Task RecalcIfNeededAsync()
    {
        if (!titleList.GameFiles.Any(f => f.IsKeyMissing))
            return;

        var tcs = new TaskCompletionSource();

        titleList.RecalcKeyMissingFiles(async () =>
        {
            await langTab.UpdateLanguageTabAsync(titleList.GameFiles);
            tcs.TrySetResult();
        });

        await tcs.Task;
    }

    private void TxtOutputPath_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && Directory.Exists(paths[0]))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void TxtOutputPath_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            {
                string droppedPath = paths[0];
                string? folderPath = Directory.Exists(droppedPath) ? droppedPath : Path.GetDirectoryName(droppedPath);

                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    ViewModel.OutputPath = folderPath;

            }
        }

        e.Handled = true;
    }
}