using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;
using Inline = Avalonia.Controls.Documents.Inline;
using MarkdownInline = Markdig.Syntax.Inlines.Inline;

namespace Datapoint.Terminal.Markdown;

internal sealed partial class MarkdownBuilder
{
    private SelectableTextBlock TextFor(ContainerInline? content)
    {
        var inlines = new InlineCollection();
        if (content is not null)
        {
            inlines.AddRange(Inlines(content));
        }

        var text = NewText(null);
        text.Inlines = inlines;
        return text;
    }

    private List<Inline> Inlines(ContainerInline container)
    {
        var root = new List<Inline>();
        var open = new Stack<(string Name, Span Span)>();
        foreach (var inline in container)
        {
            if (inline is HtmlInline html && HtmlTag.ReadFormatting(html.Tag) is { } tag)
            {
                Apply(tag, open, root);
            }
            else if (Inline(inline) is { } converted)
            {
                Add(open, root, converted);
            }
        }

        return root;
    }

    private void Apply(HtmlTag tag, Stack<(string Name, Span Span)> open, List<Inline> root)
    {
        if (tag.IsBreak)
        {
            Add(open, root, new Run("\n"));
        }
        else if (!tag.IsClosing)
        {
            var span = HtmlSpan(tag.Name);
            Add(open, root, span);
            open.Push((tag.Name, span));
        }
        else if (open.Any(item => string.Equals(item.Name, tag.Name, StringComparison.Ordinal)))
        {
            while (!string.Equals(open.Pop().Name, tag.Name, StringComparison.Ordinal))
            {
            }
        }
    }

    private static void Add(Stack<(string Name, Span Span)> open, List<Inline> root, Inline inline)
    {
        if (open.Count > 0)
        {
            open.Peek().Span.Inlines.Add(inline);
            return;
        }

        root.Add(inline);
    }

    private Span HtmlSpan(string name) => name switch
    {
        "b" or "strong" => new Bold(),
        "i" or "em" => new Italic(),
        "u" or "ins" => new Span { TextDecorations = TextDecorations.Underline },
        "s" or "del" or "strike" => new Span { TextDecorations = TextDecorations.Strikethrough },
        "sub" => new Span { BaselineAlignment = BaselineAlignment.Subscript, FontSize = 10 },
        "sup" => new Span { BaselineAlignment = BaselineAlignment.Superscript, FontSize = 10 },
        "small" => new Span { FontSize = 11 },
        "mark" => Brush(new Span(), TextElement.BackgroundProperty, "FillBrush"),
        _ => Mono(new Span()),
    };

    private Inline? Inline(MarkdownInline inline) => inline switch
    {
        MathInline math => new Italic { Inlines = { new Run(MathText.ToUnicode(math.Content.ToString())) } },
        LiteralInline literal => new Run(literal.PreviousSibling is TaskList ? literal.Content.ToString().TrimStart() : literal.Content.ToString()),
        CodeInline code => Mono(new Run(code.Content)),
        LineBreakInline lineBreak => new Run(lineBreak.IsHard ? "\n" : " "),
        EmphasisInline emphasis => Emphasis(emphasis),
        LinkInline link => Link(link),
        _ => Other(inline),
    };

    private Inline? Other(MarkdownInline inline) => inline switch
    {
        AutolinkInline autolink => Link(autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url, [new Run(autolink.Url)]),
        FootnoteLink { IsBackLink: false } footnote => Superscript(footnote.Footnote.Order),
        FootnoteLink or TaskList => null,
        HtmlEntityInline entity => new Run(entity.Transcoded.ToString()),
        HtmlInline html => new Run(html.Tag),
        ContainerInline other => Span(Inlines(other)),
        _ => null,
    };

    private T Mono<T>(T inline)
        where T : Inline
    {
        inline.FontSize = 12;
        Brush(inline, TextElement.FontFamilyProperty, "MonoFont");
        return Brush(inline, TextElement.BackgroundProperty, "FillBrush");
    }

    private Run Superscript(int order) =>
        Brush(new Run(order.ToString(CultureInfo.InvariantCulture)) { BaselineAlignment = BaselineAlignment.Superscript, FontSize = 10 }, TextElement.ForegroundProperty, "AccentBrush");

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

    private Span? Link(LinkInline link)
    {
        if (!link.IsImage)
        {
            return Link(link.Url ?? string.Empty, Inlines(link), showAddress: !link.IsAutoLink);
        }

        var description = string.Concat(InlineText.Runs(Span(Inlines(link)).Inlines).Select(item => item.Run.Text));
        _images.Add(new MarkdownImage(link.Url, description));
        return null;
    }

    // The address follows a label that says something else, so a misleading label can't hide where it points. A bare domain made
    // into a link (www.example.com) points where it says, with http:// added, so it needs none.
    private Span Link(string url, List<Inline> label, bool showAddress = true)
    {
        var target = LinkSpan.Openable(url);
        var span = target is null ? new Span() : Brush(new LinkSpan(target), TextElement.ForegroundProperty, "AccentBrush");
        span.TextDecorations = TextDecorations.Underline;
        span.Inlines.AddRange(label);
        var labelText = string.Concat(InlineText.Runs(span.Inlines).Select(item => item.Run.Text));
        if (!showAddress || url.Length == 0 || string.Equals(labelText, url, StringComparison.Ordinal) || string.Equals("mailto:" + labelText, url, StringComparison.Ordinal))
        {
            return span;
        }

        var address = Brush(new Run($" ({url})"), TextElement.ForegroundProperty, "SecondaryLabelBrush");
        return new Span { Inlines = { span, address } };
    }

    private static Span Span(IEnumerable<Inline> inlines)
    {
        var span = new Span();
        span.Inlines.AddRange(inlines);
        return span;
    }
}
