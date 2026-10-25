using System.Windows;
using System.Windows.Controls;

namespace NSW.Core.UI;

public partial class IconButton : UserControl
{
    public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(nameof(IsRunning), typeof(bool), typeof(IconButton), new PropertyMetadata(false));

    public static readonly DependencyProperty DefaultTextProperty = DependencyProperty.Register(nameof(DefaultText), typeof(string), typeof(IconButton), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CancelTextProperty = DependencyProperty.Register(nameof(CancelText), typeof(string), typeof(IconButton), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DefaultIconProperty = DependencyProperty.Register(nameof(DefaultIcon), typeof(string), typeof(IconButton), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CancelIconProperty = DependencyProperty.Register(nameof(CancelIcon), typeof(string), typeof(IconButton), new PropertyMetadata(string.Empty));

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public string DefaultText
    {
        get => (string)GetValue(DefaultTextProperty);
        set => SetValue(DefaultTextProperty, value);
    }

    public string CancelText
    {
        get => (string)GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    public string DefaultIcon
    {
        get => (string)GetValue(DefaultIconProperty);
        set => SetValue(DefaultIconProperty, value);
    }

    public string CancelIcon
    {
        get => (string)GetValue(CancelIconProperty);
        set => SetValue(CancelIconProperty, value);
    }

    public event RoutedEventHandler? Click;

    public IconButton() => InitializeComponent();

    private void InnerButton_Click(object sender, RoutedEventArgs e) => Click?.Invoke(this, e);
}