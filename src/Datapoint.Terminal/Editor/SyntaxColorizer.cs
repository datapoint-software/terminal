using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Datapoint.Terminal.Markdown;

namespace Datapoint.Terminal.Editor;

// Colours the editor's lines from spans worked out for the whole text, so strings and comments that run over several lines are
// coloured as one. While the user types, the spans are a moment behind; they're kept within each line so stale ones can't fail.
internal sealed class SyntaxColorizer(Func<SyntaxKind, IBrush?> brushFor) : DocumentColorizingTransformer
{
    private IReadOnlyList<(int Start, int End, SyntaxKind Kind)> _spans = [];

    public static IReadOnlyList<(int Start, int End, SyntaxKind Kind)> Spans(string text, string? language)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (language is null)
        {
            return [];
        }

        var spans = new List<(int Start, int End, SyntaxKind Kind)>();
        var position = 0;
        foreach (var (token, kind) in SyntaxHighlighter.Highlight(text, language, SyntaxHighlighter.MaxFileLength))
        {
            if (kind != SyntaxKind.Plain)
            {
                spans.Add((position, position + token.Length, kind));
            }

            position += token.Length;
        }

        return spans;
    }

    public void SetSpans(IReadOnlyList<(int Start, int End, SyntaxKind Kind)> spans) => _spans = spans;

    protected override void ColorizeLine(DocumentLine line)
    {
        var length = CurrentContext.Document.TextLength;
        var lineEnd = Math.Min(line.EndOffset, length);
        for (var index = First(line.Offset); index < _spans.Count && _spans[index].Start < lineEnd; index++)
        {
            var (start, end, kind) = _spans[index];
            var from = Math.Max(start, line.Offset);
            var to = Math.Min(end, lineEnd);
            if (to <= from || brushFor(kind) is not { } brush)
            {
                continue;
            }

            ChangeLinePart(from, to, element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    // The first span that ends after the line starts.
    private int First(int offset)
    {
        var low = 0;
        var high = _spans.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (_spans[middle].End <= offset)
            {
                low = middle + 1;
                continue;
            }

            high = middle;
        }

        return low;
    }
}
