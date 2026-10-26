using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls.Switch;

public partial class SwitchDropHint : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(SwitchDropHint), new PropertyMetadata("파일 또는 폴더를 여기에 드래그&드롭하세요"));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(SwitchDropHint), new PropertyMetadata(string.Empty, OnSubtitleChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public SwitchDropHint() => InitializeComponent();

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SwitchDropHint hint)
            hint.txtSubtitle.Visibility = string.IsNullOrEmpty(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
    }
}