using RomForge.Core.Models.Naming;
using RomForge.Core.Services.Naming;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RomForge.Controls.Naming;

public partial class NamingFormatEditor : UserControl
{
    private static readonly BitmapImage ClearIcon = LoadIcon("Clear.png");

    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(string), typeof(NamingFormatEditor), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnFormatChanged));

    public static readonly DependencyProperty TokensProperty = DependencyProperty.Register(nameof(Tokens), typeof(IReadOnlyList<NamingTokenDefinition>), typeof(NamingFormatEditor), new PropertyMetadata(Array.Empty<NamingTokenDefinition>(), OnTokensChanged));

    private bool _syncing;

    public NamingFormatEditor()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(editor, OnPasting);
    }

    public string Format
    {
        get => (string)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public IReadOnlyList<NamingTokenDefinition> Tokens
    {
        get => (IReadOnlyList<NamingTokenDefinition>)GetValue(TokensProperty);
        set => SetValue(TokensProperty, value);
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NamingFormatEditor)d;

        if (!control._syncing)
            control.Rebuild();
    }

    private static void OnTokensChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NamingFormatEditor)d;

        control.BuildPalette();
        control.Rebuild();
    }

    private static string StripLineBreaks(string? text) => (text ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

    private static Paragraph CreateParagraph() => new() { Margin = new Thickness(0) };

    private void BuildPalette()
    {
        palette.Children.Clear();

        foreach (var definition in Tokens)
            palette.Children.Add(new NamingBadge(definition, definition.DefaultOption?.Value) { Margin = new Thickness(0, 0, 4, 4) });
    }

    private void Rebuild()
    {
        _syncing = true;

        try
        {
            var paragraph = CreateParagraph();

            foreach (var segment in NamingFormatSerializer.Parse(StripLineBreaks(Format), Tokens))
            {
                if (segment.Token is null)
                    paragraph.Inlines.Add(new Run(segment.Text));
                else
                    paragraph.Inlines.Add(CreateContainer(new NamingBadge(segment.Token, segment.Option), null));
            }

            editor.Document.Blocks.Clear();
            editor.Document.Blocks.Add(paragraph);
        }
        finally
        {
            _syncing = false;
        }
    }

    private InlineUIContainer CreateContainer(NamingBadge badge, TextPointer? position)
    {
        var container = position is null ? new InlineUIContainer(badge) : new InlineUIContainer(badge, position);
        container.BaselineAlignment = BaselineAlignment.Center;
        badge.Host = container;
        badge.ContextMenu = CreateMenu(badge);

        return container;
    }

    private ContextMenu CreateMenu(NamingBadge badge)
    {
        var menu = new ContextMenu();
        var optionItems = new List<MenuItem>();

        foreach (var option in badge.Definition.Options ?? [])
        {
            var item = new MenuItem { Header = option.MenuLabel, Tag = option };

            item.Click += (_, _) =>
            {
                badge.Option = option.Value;

                badge.Refresh();
                UpdateChecks(optionItems, badge);
                Commit();
            };

            optionItems.Add(item);
            menu.Items.Add(item);
        }

        if (optionItems.Count > 0)
        {
            UpdateChecks(optionItems, badge);

            menu.Items.Add(new Separator());
        }

        var remove = new MenuItem { Header = "삭제", Icon = new Image { Source = ClearIcon, Width = 20, Height = 20 } };

        remove.Click += (_, _) => RemoveBadge(badge);

        menu.Items.Add(remove);

        return menu;
    }

    private static void UpdateChecks(List<MenuItem> items, NamingBadge badge)
    {
        foreach (var item in items)
            item.IsChecked = string.Equals(((NamingTokenOption)item.Tag).Value, badge.Option, StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveBadge(NamingBadge badge)
    {
        if (badge.Host is not { } host)
            return;

        badge.Host = null;
        badge.ContextMenu = null;

        DeleteContainer(host);
    }

    private static void DeleteContainer(InlineUIContainer container)
    {
        if (container.Parent is null)
            return;

        new TextRange(container.ElementStart, container.ElementEnd).Text = string.Empty;
    }

    private TextPointer ResolvePosition(DragEventArgs e) => (editor.GetPositionFromPoint(e.GetPosition(editor), true) ?? editor.Document.ContentEnd).GetInsertionPosition(LogicalDirection.Forward);

    private void Editor_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(NamingBadge.DragFormat) || NamingBadge.DragSource is not { } source)
            return;

        e.Effects = source.Host is null ? DragDropEffects.Copy : DragDropEffects.Move;
        e.Handled = true;

        editor.Focus();

        editor.CaretPosition = ResolvePosition(e);
    }

    private void Editor_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(NamingBadge.DragFormat) || NamingBadge.DragSource is not { } source)
            return;

        e.Handled = true;

        CreateContainer(new NamingBadge(source.Definition, source.Option), ResolvePosition(e));

        if (source.Host is { } old)
        {
            source.Host = null;

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => DeleteContainer(old)));
        }
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            e.Handled = true;
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e) => Commit();

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.SourceDataObject.GetData(DataFormats.UnicodeText) is not string text)
        {
            e.CancelCommand();
            return;
        }

        e.DataObject = new DataObject(DataFormats.UnicodeText, StripLineBreaks(text));
        e.FormatToApply = DataFormats.UnicodeText;
    }

    private void Commit()
    {
        if (_syncing)
            return;

        _syncing = true;

        try
        {
            Format = NamingFormatSerializer.Serialize(editor.Document.Blocks.OfType<Paragraph>().SelectMany(p => Collect(p.Inlines)));
        }
        finally
        {
            _syncing = false;
        }
    }

    private static IEnumerable<NamingFormatSegment> Collect(IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run)
                yield return NamingFormatSegment.Literal(StripLineBreaks(run.Text));
            else if (inline is InlineUIContainer { Child: NamingBadge badge })
                yield return badge.ToSegment();
            else if (inline is Span span)
            {
                foreach (var segment in Collect(span.Inlines))
                    yield return segment;
            }
        }
    }

    private static BitmapImage LoadIcon(string name)
    {
        var image = new BitmapImage(new Uri($"pack://application:,,,/Assets/Images/{name}"));
        image.Freeze();

        return image;
    }
}