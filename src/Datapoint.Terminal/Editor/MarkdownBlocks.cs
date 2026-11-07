using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace Datapoint.Terminal.Editor;

// Splits a Markdown file into the pieces the editor shows and edits one at a time: each top-level block, and each item of a
// top-level list. Joined back together, the pieces are the file exactly as it was, so a part the user doesn't touch is
// written back unchanged, including the hidden markers around the agent's sections (product decision 98).
public static class MarkdownBlocks
{
    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .Build();

    public static IReadOnlyList<MarkdownBlock> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var document = Markdig.Markdown.Parse(text, s_pipeline);
        var starts = new List<(int Start, BlockKind Kind)>();
        foreach (var block in document)
        {
            if (block is ListBlock list)
            {
                starts.AddRange(list.OfType<ListItemBlock>().Select(item => (LineStart(text, item.Span.Start), KindOf(item, list))));
                continue;
            }

            starts.Add((LineStart(text, block.Span.Start), KindOf(block, list: null)));
        }

        return Regions(text, starts);
    }

    // What one piece of edited text has become, e.g. two paragraphs where there was one.
    public static BlockKind KindOf(string body) => Split(body) switch
    {
        [] => BlockKind.Gap,
        [var only] => only.Kind,
        [var first, ..] => first.Kind,
    };

    private static List<MarkdownBlock> Regions(string text, List<(int Start, BlockKind Kind)> starts)
    {
        var blocks = new List<MarkdownBlock>();
        var distinct = starts.DistinctBy(start => start.Start).OrderBy(start => start.Start).ToList();
        if (distinct.Count == 0 || distinct[0].Start > 0)
        {
            // Blank lines before the first block, or a file with nothing but blank lines.
            var end = distinct.Count == 0 ? text.Length : distinct[0].Start;
            if (end > 0)
            {
                blocks.Add(new MarkdownBlock(string.Empty, text[..end], BlockKind.Gap));
            }
        }

        for (var index = 0; index < distinct.Count; index++)
        {
            var start = distinct[index].Start;
            var end = index + 1 < distinct.Count ? distinct[index + 1].Start : text.Length;
            var region = text[start..end];
            var body = region.TrimEnd('\r', '\n', ' ', '\t');
            body = body.Length == 0 ? body : region[..(body.Length + TrailingSpacesOnLastLine(region, body.Length))];
            blocks.Add(new MarkdownBlock(body, region[body.Length..], distinct[index].Kind));
        }

        return blocks;
    }

    // Spaces at the end of a block's last line belong to it (two of them make a line break), not to the gap after it.
    private static int TrailingSpacesOnLastLine(string region, int bodyLength)
    {
        var count = 0;
        while (bodyLength + count < region.Length && region[bodyLength + count] is ' ' or '\t')
        {
            count++;
        }

        return count;
    }

    private static int LineStart(string text, int position)
    {
        var at = Math.Clamp(position, 0, text.Length);
        while (at > 0 && text[at - 1] != '\n')
        {
            at--;
        }

        return at;
    }

    private static BlockKind KindOf(Block block, ListBlock? list) => block switch
    {
        ListItemBlock item when item.FirstOrDefault() is ParagraphBlock { Inline.FirstChild: Markdig.Extensions.TaskLists.TaskList } => BlockKind.Task,
        ListItemBlock when list?.IsOrdered == true => BlockKind.Numbered,
        ListItemBlock => BlockKind.Bullet,
        HeadingBlock { Level: 1 } => BlockKind.Heading1,
        HeadingBlock { Level: 2 } => BlockKind.Heading2,
        HeadingBlock => BlockKind.Heading3,
        ParagraphBlock => BlockKind.Paragraph,
        QuoteBlock => BlockKind.Quote,
        FencedCodeBlock or CodeBlock => BlockKind.Code,
        Table => BlockKind.Table,
        HtmlBlock html when IsComment(html) => BlockKind.Hidden,
        _ => BlockKind.Other,
    };

    // Comments, such as the markers around the agent's sections, are kept but never shown.
    private static bool IsComment(HtmlBlock html)
    {
        var source = html.Lines.ToString().Trim();
        return source.StartsWith("<!--", StringComparison.Ordinal) && source.EndsWith("-->", StringComparison.Ordinal);
    }
}
