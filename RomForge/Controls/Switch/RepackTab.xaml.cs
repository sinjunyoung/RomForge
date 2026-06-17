using Common;
using NSW.Core.Enums;
using NSW.M1.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Path = System.IO.Path;

namespace RomForge.Controls.Switch;

public partial class RepackTab : UserControl
{
    RepackMainViewModel ViewModel => (RepackMainViewModel)DataContext;

    public RepackTab()
    {
        InitializeComponent();
        Loaded += RepackTab_Loaded;
    }

    private void RepackTab_Loaded(object sender, RoutedEventArgs e)
    {
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            return;

        ViewModel.OnLogRequest += (msg, level) => Dispatcher.Invoke(() => Log(msg, level));
        ViewModel.OnWorkingChanged += (isWorking, mode) => Dispatcher.Invoke(() => SetWorking(isWorking, mode));
        fileMgr.FileListChanged += SyncAll;

        SyncAll();
    }

    private void SyncAll()
    {
        langTab.UpdateLanguageTab(fileMgr.GameFiles, Path.Combine(ViewModel.OutputPath, "unpacked"));
        SyncContext();
    }

    private void SyncContext()
    {
        ViewModel.Context = new RepackMainViewModel.BuildContext(
            fileMgr.GameFiles,
            langTab.CurrentMetadata,
            langTab.ForcedLanguage);
    }

    private void TxtPatch_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void TxtPatch_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void TxtPatch_Drop(object sender, DragEventArgs e)
    {
        var items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var folder = items?.FirstOrDefault(Directory.Exists);
        if (folder != null) ViewModel.PatchPath = folder;
        e.Handled = true;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsWorking) { ViewModel.Cancel(); return; }
        await RecalcIfNeededAsync();
        langTab.SyncMetadataFromUI();
        SyncContext();
        await ViewModel.StartAsync(BuildMode.FullProcess);
    }

    private async void BtnUnpack_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsWorking) { ViewModel.Cancel(); return; }
        await RecalcIfNeededAsync();
        SyncContext();
        await ViewModel.StartAsync(BuildMode.UnpackOnly);
    }

    private void BtnRebuild_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsWorking) { ViewModel.Cancel(); return; }
        SyncContext();
        _ = ViewModel.StartAsync(BuildMode.RebuildOnly);
    }

    private async Task RecalcIfNeededAsync()
    {
        if (!fileMgr.GameFiles.Any(f => f.IsKeyMissing)) return;

        var tcs = new TaskCompletionSource();
        fileMgr.RecalcKeyMissingFiles(() =>
        {
            langTab.UpdateLanguageTab(fileMgr.GameFiles);
            tcs.TrySetResult();
        });
        await tcs.Task;
    }

    private void Log(string msg, LogLevel level = LogLevel.Info)
    {
        Color color = level switch
        {
            LogLevel.Error => Color.FromRgb(255, 80, 80),
            LogLevel.Ok => Color.FromRgb(100, 200, 100),
            LogLevel.Highlight => Color.FromRgb(255, 200, 0),
            _ => Color.FromRgb(180, 180, 180),
        };

        Dispatcher.Invoke(() =>
        {
            var para = new Paragraph(new Run(msg))
            {
                Foreground = new SolidColorBrush(color),
                Margin = new Thickness(0),
                LineHeight = 18,
            };
            //logBox.Document.Blocks.Add(para);
            //logBox.ScrollToEnd();
        });
    }

    private void SetWorking(bool working, BuildMode mode)
    {
        //fileMgr.IsEnabled = !working;
        //btnBrowsePatch.IsEnabled = !working;
        //btnBrowseOutput.IsEnabled = !working;
        //langTab.IsEnabled = !working;
        //txtPatch.IsEnabled = !working;
        //txtOutput.IsEnabled = !working;

        //UpdateStaticButtonUI(btnUnpack, working && mode == BuildMode.UnpackOnly, "언팩");
        //UpdateStaticButtonUI(btnRebuild, working && mode == BuildMode.RebuildOnly, "리팩");
        //UpdateStaticButtonUI(btnStart, working && mode == BuildMode.FullProcess, "언팩 + 리팩 (Full)");

        //btnUnpack.IsEnabled = !working || mode == BuildMode.UnpackOnly;
        //btnRebuild.IsEnabled = !working || mode == BuildMode.RebuildOnly;
        //btnStart.IsEnabled = !working || mode == BuildMode.FullProcess;

        //progressArea.Visibility = working ? Visibility.Visible : Visibility.Collapsed;
    }
}