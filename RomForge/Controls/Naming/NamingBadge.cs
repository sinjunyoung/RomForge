using RomForge.Core.Models.Naming;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace RomForge.Controls.Naming;

public sealed class NamingBadge : Border
{
    public const string DragFormat = "RomForge.NamingBadge";

    private readonly TextBlock _label = new()
    {
        Foreground = Brushes.White,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center
    };

    private Point _dragStart;
    private bool _pressed;

    public NamingBadge(NamingTokenDefinition definition, string? option)
    {
        Definition = definition;
        Option = option;
        Background = new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xA8));
        CornerRadius = new CornerRadius(9);
        Padding = new Thickness(8, 1, 8, 1);
        Margin = new Thickness(2, 0, 2, 0);
        Cursor = Cursors.Hand;
        ToolTip = definition.ToolTip;
        Child = _label;
        Refresh();
    }

    public static NamingBadge? DragSource { get; private set; }

    public NamingTokenDefinition Definition { get; }

    public string? Option { get; set; }

    public InlineUIContainer? Host { get; set; }

    public NamingFormatSegment ToSegment() => NamingFormatSegment.ForToken(Definition, Option);

    public void Refresh()
    {
        string suffix = ResolveSuffix();

        _label.Text = suffix.Length == 0 ? Definition.Label : $"{Definition.Label}({suffix})";
    }

    private string ResolveSuffix()
    {
        var match = Definition.Options?.FirstOrDefault(o => string.Equals(o.Value, Option, StringComparison.OrdinalIgnoreCase));
        return match?.BadgeSuffix ?? Option ?? string.Empty;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _pressed = true;
        e.Handled = true;

        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _pressed = false;

        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_pressed || e.LeftButton != MouseButtonState.Pressed)
            return;

        Point position = e.GetPosition(null);

        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _pressed = false;
        DragSource = this;

        try
        {
            DragDrop.DoDragDrop(this, new DataObject(DragFormat, Definition.Key), DragDropEffects.Copy | DragDropEffects.Move);
        }
        finally
        {
            DragSource = null;
        }
    }
}