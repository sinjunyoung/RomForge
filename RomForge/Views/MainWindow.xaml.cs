using NSW.WPF.UI;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.Services;
using RomForge.Core.Services.Util;
using RomForge.Core.UI.Helpers;
using RomForge.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace RomForge.Views;

public partial class MainWindow : Window
{

    private MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;

        RestoreWindowState();

        Topmost = AppConfig.Instance.Common.Topmost;
        AppConfig.Instance.Common.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CommonConfig.Topmost))
                Topmost = AppConfig.Instance.Common.Topmost;
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!AppConfig.Instance.Common.AutoCheckUpdate)
            return;

        try
        {
            var isUpdateAvailable = await VersionHelper.IsUpdateAvailableAsync();

            if (isUpdateAvailable)
            {
                var result = MessageBoxHelper.ShowQuestion("새 버전이 있습니다. 다운로드 페이지를 여시겠습니까?");

                if (result == true)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://github.com/sinjunyoung/RomForge/releases/latest",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBoxHelper.ShowError($"다운로드 페이지를 열 수 없습니다.\n\n오류 내용: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBoxHelper.ShowError($"업데이트 정보를 확인할 수 없습니다.\n\n오류 내용: {ex.Message}");
        }
    }

    private void RestoreWindowState()
    {
        var cfg = AppConfig.Instance.Window;

        Left = cfg.Left;
        Top = cfg.Top;
        Width = cfg.Width;
        Height = cfg.Height;

        if (cfg.IsMaximized)
            WindowState = WindowState.Maximized;
    }

    private void SaveWindowState()
    {
        var cfg = AppConfig.Instance.Window;

        cfg.IsMaximized = WindowState == WindowState.Maximized;

        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);

        cfg.Left = bounds.Left;
        cfg.Top = bounds.Top;
        cfg.Width = bounds.Width;
        cfg.Height = bounds.Height;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;
        int value = 1;

        _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
    }

    private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e) => MainViewModel.LogBoxHeight = LogRow.Height.Value;

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowState();
        MainViewModel.SaveConfig();

        bool busy = ViewModel.IsAnyChildLocked();

        if (!busy)
            return;

        var result = MessageBoxHelper.ShowQuestion("작업이 진행 중입니다. 취소하고 종료할까요?");

        if (result)
            ViewModel.CancelAll();
        else
            e.Cancel = true;
    }

    private void LogBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (logBox.SelectedItems.Count == 0 || logBox.Items.Count == 0)
            e.Handled = true;
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearLog();
    }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        if (logBox.SelectedItems.Count > 0)
        {
            var lines = new List<string>();

            foreach (LogEntry entry in logBox.SelectedItems)
            {
                if (entry != null)
                    lines.Add(entry.Message);
            }

            if (lines.Count > 0)
                ClipboardHelper.CopyText(string.Join(Environment.NewLine, lines));
        }
    }
}