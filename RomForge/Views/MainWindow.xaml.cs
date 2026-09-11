using NSW.WPF.UI;
using RomForge.Core;
using RomForge.Core.UI.Helpers;
using RomForge.ViewModels;
using System.ComponentModel;
using System.Windows;
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

        RestoreWindowState();
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
}