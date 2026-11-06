using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Markdown;

// Only formatting tags are honoured; attributes are ignored.
internal readonly partial record struct HtmlTag(string Name, bool IsClosing)
{
    private static readonly HashSet<string> s_formatting = new(StringComparer.Ordinal)
    {
        "b", "strong", "i", "em", "u", "ins", "s", "del", "strike", "sub", "sup", "small", "mark", "code", "kbd", "samp", "br",
    };

    public bool IsBreak => string.Equals(Name, "br", StringComparison.Ordinal);

    public static HtmlTag? ReadFormatting(string html)
    {
        var match = Pattern().Match(html);
        if (!match.Success)
        {
            return null;
        }

        var name = match.Groups["name"].Value.ToLowerInvariant();
        return s_formatting.Contains(name) ? new HtmlTag(name, match.Groups["closing"].Length > 0) : null;
    }

    [GeneratedRegex(@"^<(?<closing>/?)(?<name>[A-Za-z][A-Za-z0-9]*)(?:\s[^<>]*)?/?>$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Pattern();
}
