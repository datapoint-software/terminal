using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;

namespace Datapoint.Terminal.Markdown;

// Only formatting HTML tags are honoured; other HTML is shown as written, never run.
internal sealed partial class MarkdownBuilder(Control owner)
{
    private const double LineHeight = 19;

    private static readonly string[] s_bullets = ["•", "◦", "▪"];

    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseTaskLists()
        .UseFootnotes()
        .UseMathematics()
        .UseAutoLinks()
        .Build();

    private FontWeight _weight = FontWeight.Normal;
    private TextAlignment _alignment = TextAlignment.Left;
    private int _depth;
    private List<MarkdownImage> _images = [];

    public List<Control> Build(string markdown) => Blocks(Markdig.Markdown.Parse(markdown, s_pipeline));

    private List<Control> Blocks(ContainerBlock container) => [.. container.Select(Block).OfType<Control>()];

    private Control? Block(Block block) => block switch
    {
        ListBlock list => List(list),
        QuoteBlock quote => Quote(quote),
        Table table => Table(table),
        FootnoteGroup footnotes => Footnotes(footnotes),
        LinkReferenceDefinitionGroup => null,
        ContainerBlock other => Stack(Blocks(other), 8),
        LeafBlock leaf => Leaf(leaf),
        _ => null,
    };

    private Control? Leaf(LeafBlock leaf) => leaf switch
    {
        HeadingBlock heading => Heading(heading),
        ParagraphBlock paragraph => Paragraph(paragraph),
        MathBlock math => DisplayMath(math.Lines.ToString()),
        CodeBlock code => Code(code),
        ThematicBreakBlock => Rule(),
        LinkReferenceDefinition => null,
        _ => NewText(leaf.Lines.ToString()),
    };

    private SelectableTextBlock NewText(string? text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = LineHeight,
        FontWeight = _weight,
        TextAlignment = _alignment,
        ContextFlyout = null,
    };

    private Control Paragraph(ParagraphBlock paragraph)
    {
        if (paragraph.Inline?.FirstChild is MathInline { DelimiterCount: 2, NextSibling: null } display)
        {
            return DisplayMath(display.Content.ToString());
        }

        _images = [];
        var text = TextFor(paragraph.Inline);
        if (_images.Count == 0)
        {
            return text;
        }

        var panel = Stack([], 6);
        if (!string.IsNullOrWhiteSpace(InlineText.Of(text)))
        {
            panel.Children.Add(text);
        }

        panel.Children.AddRange(_images);
        return panel;
    }

    private SelectableTextBlock Heading(HeadingBlock heading)
    {
        var text = TextFor(heading.Inline);
        text.FontWeight = FontWeight.SemiBold;
        text.FontSize = heading.Level switch
        {
            1 => 18,
            2 => 16,
            _ => 14,
        };
        text.LineHeight = double.NaN;
        text.Margin = new Thickness(0, 4, 0, 0);
        return text;
    }

    private Rectangle Rule() => Brush(new Rectangle { Height = 1, Margin = new Thickness(0, 4) }, Shape.FillProperty, "SeparatorBrush");

    private Border Quote(QuoteBlock quote)
    {
        var border = new Border { BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 0, 0, 0), Child = Stack(Blocks(quote), 8) };
        Brush(border.Child, Avalonia.Controls.Documents.TextElement.ForegroundProperty, "SecondaryLabelBrush");
        return Brush(border, Border.BorderBrushProperty, "SeparatorBrush");
    }

    private StackPanel List(ListBlock list)
    {
        var panel = new StackPanel { Spacing = list.IsLoose ? 8 : 2 };
        var number = list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ? start : 1;
        _depth++;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            panel.Children.Add(Row(Marker(list, item, ref number), Stack(Blocks(item), 4)));
        }

        _depth--;
        return panel;
    }

    private Control Marker(ListBlock list, ListItemBlock item, ref int number) => item.FirstOrDefault() switch
    {
        ParagraphBlock { Inline.FirstChild: TaskList task } => TaskBox(task.Checked),
        _ when list.IsOrdered => MarkerText(string.Create(CultureInfo.InvariantCulture, $"{number++}{list.OrderedDelimiter}")),
        _ => MarkerText(s_bullets[(_depth - 1) % s_bullets.Length]),
    };

    private static TextBlock MarkerText(string text) => new() { Text = text, MinWidth = 14, Margin = new Thickness(2, 0, 6, 0), LineHeight = LineHeight };

    // Read-only: the reply reports the state, it isn't a form.
    private static LayoutTransformControl TaskBox(bool isChecked) => new()
    {
        LayoutTransform = new ScaleTransform(0.7, 0.7),
        Margin = new Thickness(0, 0, 4, 0),
        VerticalAlignment = VerticalAlignment.Top,
        Child = new CheckBox { IsChecked = isChecked, IsHitTestVisible = false, Focusable = false, MinWidth = 0, MinHeight = 0, Padding = default },
    };

    private static Grid Row(Control marker, Control body)
    {
        Grid.SetColumn(body, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { marker, body } };
    }

    private ScrollViewer Table(Table table)
    {
        var rows = table.OfType<TableRow>().ToList();
        var grid = new Grid();
        for (var column = 0; column < rows.Select(row => row.Count).DefaultIfEmpty(0).Max(); column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        for (var index = 0; index < rows.Count; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddRow(grid, table, rows[index], index);
        }

        return new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    private void AddRow(Grid grid, Table table, TableRow row, int index)
    {
        _weight = row.IsHeader ? FontWeight.SemiBold : FontWeight.Normal;
        for (var column = 0; column < row.Count; column++)
        {
            _alignment = Alignment(table, column);
            var cell = new Border { Child = Stack(Blocks((TableCell)row[column]), 4), Padding = new Thickness(0, 4, 14, 4), BorderThickness = new Thickness(0, 0, 0, 1) };
            Brush(cell, Border.BorderBrushProperty, "SeparatorBrush");
            Grid.SetRow(cell, index);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        _weight = FontWeight.Normal;
        _alignment = TextAlignment.Left;
    }

    private static TextAlignment Alignment(Table table, int column) =>
        column < table.ColumnDefinitions.Count
            ? table.ColumnDefinitions[column].Alignment switch
            {
                TableColumnAlign.Center => TextAlignment.Center,
                TableColumnAlign.Right => TextAlignment.Right,
                _ => TextAlignment.Left,
            }
            : TextAlignment.Left;

    private StackPanel Footnotes(FootnoteGroup group)
    {
        var panel = new StackPanel { Spacing = 4, Children = { Rule() } };
        foreach (var note in group.OfType<Footnote>())
        {
            var marker = MarkerText(string.Create(CultureInfo.InvariantCulture, $"{note.Order}."));
            panel.Children.Add(Row(Brush(marker, TextBlock.ForegroundProperty, "SecondaryLabelBrush"), Stack(Blocks(note), 4)));
        }

        return panel;
    }

    private static StackPanel Stack(List<Control> children, double spacing)
    {
        var panel = new StackPanel { Spacing = spacing, VerticalAlignment = VerticalAlignment.Top };
        panel.Children.AddRange(children);
        return panel;
    }

    private T Brush<T>(T target, AvaloniaProperty property, string key)
        where T : AvaloniaObject
    {
        target.Bind(property, owner.GetResourceObservable(key));
        return target;
    }
}
