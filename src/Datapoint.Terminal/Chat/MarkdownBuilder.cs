using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Inline = Avalonia.Controls.Documents.Inline;
using MarkdownInline = Markdig.Syntax.Inlines.Inline;

namespace Datapoint.Terminal.Chat;

/// <summary>
/// Turns Markdown into Avalonia controls: one selectable text block per paragraph, and panels for lists, quotes,
/// code and tables. Raw HTML is shown as text, never interpreted.
/// </summary>
internal sealed class MarkdownBuilder(Control owner)
{
    private const double LineHeight = 19;

    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
        .DisableHtml()
        .Build();

    private FontWeight _weight = FontWeight.Normal;

    public List<Control> Build(string markdown) => Blocks(Markdig.Markdown.Parse(markdown, s_pipeline));

    private List<Control> Blocks(ContainerBlock container) => [.. container.Select(Block).OfType<Control>()];

    private Control? Block(Block block) => block switch
    {
        ListBlock list => List(list),
        QuoteBlock quote => Quote(quote),
        Table table => Table(table),
        LinkReferenceDefinitionGroup => null,
        ContainerBlock other => Stack(Blocks(other), 8),
        LeafBlock leaf => Leaf(leaf),
        _ => null,
    };

    private Control? Leaf(LeafBlock leaf) => leaf switch
    {
        HeadingBlock heading => Heading(heading),
        ParagraphBlock paragraph => Text(paragraph.Inline),
        CodeBlock code => Code(code),
        ThematicBreakBlock => Rule(),
        LinkReferenceDefinition => null,
        _ => new SelectableTextBlock { Text = leaf.Lines.ToString(), TextWrapping = TextWrapping.Wrap },
    };

    private SelectableTextBlock Text(ContainerInline? content)
    {
        var inlines = new InlineCollection();
        if (content is not null)
        {
            inlines.AddRange(Inlines(content));
        }

        return new SelectableTextBlock { Inlines = inlines, TextWrapping = TextWrapping.Wrap, LineHeight = LineHeight, FontWeight = _weight };
    }

    private SelectableTextBlock Heading(HeadingBlock heading)
    {
        var text = Text(heading.Inline);
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

    private static Border Code(CodeBlock code)
    {
        var text = new SelectableTextBlock { Text = code.Lines.ToString().TrimEnd(), TextWrapping = TextWrapping.Wrap };
        text.Classes.Add("mono");
        var border = new Border { Child = text };
        border.Classes.Add("code");
        return border;
    }

    private Rectangle Rule()
    {
        var rule = new Rectangle { Height = 1, Margin = new Thickness(0, 4) };
        rule.Bind(Shape.FillProperty, owner.GetResourceObservable("SeparatorBrush"));
        return rule;
    }

    private Border Quote(QuoteBlock quote)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 0, 0, 0),
            Child = Stack(Blocks(quote), 8),
        };
        border.Bind(Border.BorderBrushProperty, owner.GetResourceObservable("SeparatorBrush"));
        border.Child.Bind(TextElement.ForegroundProperty, owner.GetResourceObservable("SecondaryLabelBrush"));
        return border;
    }

    private StackPanel List(ListBlock list)
    {
        var panel = new StackPanel { Spacing = list.IsLoose ? 8 : 2 };
        var number = list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ? start : 1;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marker = list.IsOrdered ? string.Create(CultureInfo.InvariantCulture, $"{number++}{list.OrderedDelimiter}") : "•";
            var body = Stack(Blocks(item), 4);
            Grid.SetColumn(body, 1);
            panel.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Children = { new TextBlock { Text = marker, MinWidth = 14, Margin = new Thickness(2, 0, 6, 0), LineHeight = LineHeight }, body },
            });
        }

        return panel;
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
            AddRow(grid, rows[index], index);
        }

        return new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    private void AddRow(Grid grid, TableRow row, int index)
    {
        _weight = row.IsHeader ? FontWeight.SemiBold : FontWeight.Normal;
        for (var column = 0; column < row.Count; column++)
        {
            var cell = new Border
            {
                Child = Stack(Blocks((TableCell)row[column]), 4),
                Padding = new Thickness(0, 4, 14, 4),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            cell.Bind(Border.BorderBrushProperty, owner.GetResourceObservable("SeparatorBrush"));
            Grid.SetRow(cell, index);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        _weight = FontWeight.Normal;
    }

    private static StackPanel Stack(List<Control> children, double spacing)
    {
        var panel = new StackPanel { Spacing = spacing, VerticalAlignment = VerticalAlignment.Top };
        panel.Children.AddRange(children);
        return panel;
    }

    private IEnumerable<Inline> Inlines(ContainerInline container) => container.Select(Inline).OfType<Inline>();

    private Inline? Inline(MarkdownInline inline) => inline switch
    {
        LiteralInline literal => new Run(literal.Content.ToString()),
        CodeInline code => InlineCode(code.Content),
        LineBreakInline lineBreak => lineBreak.IsHard ? new LineBreak() : new Run(" "),
        EmphasisInline emphasis => Emphasis(emphasis),
        LinkInline link => Link(link),
        AutolinkInline autolink => Link(autolink.Url, [new Run(autolink.Url)]),
        ContainerInline other => Span(Inlines(other)),
        _ => Other(inline),
    };

    private static Run? Other(MarkdownInline inline) => inline switch
    {
        HtmlEntityInline entity => new Run(entity.Transcoded.ToString()),
        HtmlInline html => new Run(html.Tag),
        _ => null,
    };

    private Run InlineCode(string code)
    {
        var run = new Run(code) { FontSize = 12 };
        run.Bind(TextElement.FontFamilyProperty, owner.GetResourceObservable("MonoFont"));
        run.Bind(TextElement.BackgroundProperty, owner.GetResourceObservable("FillBrush"));
        return run;
    }

    private Span Emphasis(EmphasisInline emphasis)
    {
        Span span = emphasis switch
        {
            { DelimiterChar: '~' } => new Span { TextDecorations = TextDecorations.Strikethrough },
            { DelimiterCount: >= 2 } => new Bold(),
            _ => new Italic(),
        };
        span.Inlines.AddRange(Inlines(emphasis));
        return span;
    }

    private Span Link(LinkInline link)
    {
        if (link.IsImage)
        {
            var image = new Italic();
            image.Inlines.AddRange(Inlines(link));
            return image;
        }

        return Link(link.Url ?? string.Empty, [.. Inlines(link)]);
    }

    /// <summary>Shows the label, and the address after it when the label says something else, so a misleading label can't hide where it points.</summary>
    private Span Link(string url, List<Inline> label)
    {
        var span = new Span { TextDecorations = TextDecorations.Underline };
        span.Bind(TextElement.ForegroundProperty, owner.GetResourceObservable("AccentBrush"));
        span.Inlines.AddRange(label);
        var labelText = string.Concat(label.OfType<Run>().Select(run => run.Text));
        if (url.Length == 0 || string.Equals(labelText, url, StringComparison.Ordinal))
        {
            return span;
        }

        var outer = Span([span]);
        var address = new Run($" ({url})");
        address.Bind(TextElement.ForegroundProperty, owner.GetResourceObservable("SecondaryLabelBrush"));
        outer.Inlines.Add(address);
        return outer;
    }

    private static Span Span(IEnumerable<Inline> inlines)
    {
        var span = new Span();
        span.Inlines.AddRange(inlines);
        return span;
    }
}
