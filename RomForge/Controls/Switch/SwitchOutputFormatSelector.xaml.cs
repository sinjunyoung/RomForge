using RomForge.Core.Models.Switch;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls.Switch;

public partial class SwitchOutputFormatSelector : UserControl
{
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(OutputFormat), typeof(SwitchOutputFormatSelector), new FrameworkPropertyMetadata(OutputFormat.NSP, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public OutputFormat Format
    {
        get => (OutputFormat)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public SwitchOutputFormatSelector()
    {
        InitializeComponent();
    }
}