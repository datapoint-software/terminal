namespace Datapoint.Terminal.Editor;

// Where a click on the rendered text lands in the Markdown: the rendered characters are found in order in the source, skipping
// the syntax around them (markers, link addresses, escapes), so the caret goes just after the last one before the click.
public static class SourceOffset
{
    public static int Of(string markdown, string renderedPrefix)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(renderedPrefix);
        var source = 0;
        var matched = 0;
        foreach (var character in renderedPrefix)
        {
            if (char.IsWhiteSpace(character))
            {
                // Line breaks, wrapping and list indents differ between the two, so spaces only need to line up loosely.
                continue;
            }

            source = PastLinkAddress(markdown, source);
            var found = markdown.IndexOf(character, source);
            if (found < 0)
            {
                break;
            }

            source = found + 1;
            matched++;
        }

        if (matched == 0)
        {
            return FirstText(markdown);
        }

        return renderedPrefix.Length > 0 && char.IsWhiteSpace(renderedPrefix[^1]) ? AfterSpace(markdown, source) : source;
    }

    // A click on the space after a word goes after the word's closing markers and the space, e.g. after "**bold** ".
    private static int AfterSpace(string markdown, int source)
    {
        var after = PastLinkAddress(markdown, source);
        while (after < markdown.Length && markdown[after] is '*' or '_' or '~' or '`')
        {
            after++;
        }

        return after < markdown.Length && markdown[after] == ' ' ? after + 1 : source;
    }

    // A link's address isn't shown, so none of the rendered text is matched inside it.
    private static int PastLinkAddress(string markdown, int source)
    {
        if (!markdown.AsSpan(source).StartsWith("](", StringComparison.Ordinal))
        {
            return source;
        }

        var end = markdown.IndexOf(')', source + 2);
        return end < 0 ? source : end + 1;
    }

    // Before the first word, after any marker such as "# " or "- [ ] ", for a click before any text.
    private static int FirstText(string markdown)
    {
        if (ListMarker.Of(markdown) is { } marker)
        {
            return marker.Length;
        }

        var hashes = 0;
        while (hashes < markdown.Length && markdown[hashes] == '#')
        {
            hashes++;
        }

        return hashes > 0 && hashes < markdown.Length && markdown[hashes] == ' ' ? hashes + 1 : 0;
    }
}
