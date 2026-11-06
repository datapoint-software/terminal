using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Datapoint.Terminal.Markdown;

/// <summary>The text of a block's inlines, in the same order and positions the text layout uses.</summary>
internal static class InlineText
{
    public static IEnumerable<(Run Run, LinkSpan? Link)> Runs(InlineCollection inlines, LinkSpan? link = null)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    yield return (run, link);
                    break;
                case Span span:
                    foreach (var item in Runs(span.Inlines, span as LinkSpan ?? link))
                    {
                        yield return item;
                    }

                    break;
            }
        }
    }

    public static string Of(SelectableTextBlock block) =>
        block.Inlines is { Count: > 0 } inlines ? string.Concat(Runs(inlines).Select(item => item.Run.Text)) : block.Text ?? string.Empty;

    public static LinkSpan? LinkAt(SelectableTextBlock block, int index)
    {
        if (block.Inlines is not { } inlines)
        {
            return null;
        }

        var offset = 0;
        foreach (var (run, link) in Runs(inlines))
        {
            offset += run.Text?.Length ?? 0;
            if (index < offset)
            {
                return link;
            }
        }

        return null;
    }
}
