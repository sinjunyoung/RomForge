using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RomForge.Controls.Switch;

public partial class IconHeader : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(IconHeader), new PropertyMetadata(null));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(IconHeader), new PropertyMetadata(string.Empty));

    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IconHeader() => InitializeComponent();
}