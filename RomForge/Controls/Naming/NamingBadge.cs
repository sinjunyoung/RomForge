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
        FontSize = 11,
        FontWeight = FontWeights.Medium,
        VerticalAlignment = VerticalAlignment.Center,
        LineHeight = double.NaN,
        LineStackingStrategy = LineStackingStrategy.MaxHeight
    };

    private readonly Brush _normalBackground;
    private readonly Brush _hoverBackground;
    private readonly Brush _normalBorder;
    private readonly Brush _hoverBorder;
    private readonly Brush _suffixBrush;

    private Point _dragStart;
    private bool _pressed;

    public event MouseButtonEventHandler? MouseDoubleClick;

    public NamingBadge(NamingTokenDefinition definition, string? option)
    {
        Definition = definition;
        Option = option;

        Color accent = AccentFor(definition.Key);

        _normalBackground = Freeze(Color.FromArgb(0x2A, accent.R, accent.G, accent.B));
        _hoverBackground = Freeze(Color.FromArgb(0x4A, accent.R, accent.G, accent.B));
        _normalBorder = Freeze(Color.FromArgb(0x70, accent.R, accent.G, accent.B));
        _hoverBorder = Freeze(Color.FromArgb(0xDD, accent.R, accent.G, accent.B));
        _suffixBrush = Freeze(accent);
        _label.Foreground = Freeze(Lighten(accent, 0.55));
        Background = _normalBackground;
        BorderBrush = _normalBorder;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(9, 2, 9, 2);
        Margin = new Thickness(2, 0, 2, 0);
        VerticalAlignment = VerticalAlignment.Center;
        Cursor = Cursors.Hand;
        ToolTip = definition.ToolTip;
        Child = _label;

        Refresh();
    }

    public static NamingBadge? DragSource { get; private set; }

    public NamingTokenDefinition Definition { get; }

    public string? Option { get; set; }

    internal InlineUIContainer? Host { get; set; }

    public NamingFormatSegment ToSegment() => NamingFormatSegment.ForToken(Definition, Option);

    public void Refresh()
    {
        string suffix = ResolveSuffix();

        _label.Inlines.Clear();
        _label.Inlines.Add(new Run(Definition.Label));

        if (suffix.Length > 0)
            _label.Inlines.Add(new Run($" ({suffix})") { Foreground = _suffixBrush, FontWeight = FontWeights.SemiBold });
    }

    private string ResolveSuffix()
    {
        var match = Definition.Options?.FirstOrDefault(o => string.Equals(o.Value, Option, StringComparison.OrdinalIgnoreCase));
        return match?.BadgeSuffix ?? Option ?? string.Empty;
    }

    private static Color AccentFor(string key) => key.ToLowerInvariant() switch
    {
        "lan1" or "lan2" or "language" => Color.FromRgb(0x4F, 0xD1, 0xB5),
        "version" or "displayversion" => Color.FromRgb(0xF2, 0xB9, 0x4B),
        "date" => Color.FromRgb(0xB6, 0x92, 0xF6),
        "titleid" or "tag" => Color.FromRgb(0x9A, 0xA8, 0xBA),
        _ => Color.FromRgb(0x6A, 0xA9, 0xFF)
    };

    private static Color Lighten(Color color, double amount) => Color.FromRgb((byte)(color.R + (255 - color.R) * amount), (byte)(color.G + (255 - color.G) * amount), (byte)(color.B + (255 - color.B) * amount));

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);

        brush.Freeze();

        return brush;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

        Background = _hoverBackground;
        BorderBrush = _hoverBorder;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        Background = _normalBackground;
        BorderBrush = _normalBorder;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _pressed = false;
            
            MouseDoubleClick?.Invoke(this, e);

            e.Handled = true;

            base.OnMouseLeftButtonDown(e);

            return;
        }

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