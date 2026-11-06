using Common;
using System.Windows;
using System.Windows.Interop;

namespace RomForge.Views;

public partial class BaseToolWindow : Window
{
    public BaseToolWindow()
    {
        InitializeComponent();
    }

    public BaseToolWindow(object viewModel, FrameworkElement contentControl, string title, double width = 1050, double height = 650)
    {
        InitializeComponent();

        Title = title;
        Width = width;
        Height = height;
        contentControl.DataContext = viewModel;
        DataContext = viewModel;
        MainContentPresenter.Content = contentControl;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;
        int value = 1;

        _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
    }
}