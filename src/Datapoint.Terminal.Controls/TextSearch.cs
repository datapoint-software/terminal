using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Controls;

// What Find looks for in each line: text, ignoring case, or a regular expression, also ignoring case.
public sealed class TextSearch
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(250);

    private readonly string _text;
    private readonly Regex? _pattern;

    private TextSearch(string text, Regex? pattern)
    {
        _text = text;
        _pattern = pattern;
    }

    public static TextSearch ForText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return new TextSearch(text, pattern: null);
    }

    // Null when the pattern isn't a valid regular expression. Each line is searched with a time limit, so a pattern that
    // backtracks badly can't freeze the window.
    public static TextSearch? ForPattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        try
        {
            return new TextSearch(pattern, new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, PatternTimeout));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // Matches that are empty, such as "^" alone, are skipped: there's nothing to select.
    internal IEnumerable<(int Index, int Length)> In(string line)
    {
        if (_pattern is null)
        {
            for (var index = line.IndexOf(_text, StringComparison.OrdinalIgnoreCase); index >= 0; index = line.IndexOf(_text, index + 1, StringComparison.OrdinalIgnoreCase))
            {
                yield return (index, _text.Length);
            }

            yield break;
        }

        MatchCollection matches;
        try
        {
            matches = _pattern.Matches(line);
            _ = matches.Count;
        }
        catch (RegexMatchTimeoutException)
        {
            yield break;
        }

        foreach (Match match in matches)
        {
            if (match.Length > 0)
            {
                yield return (match.Index, match.Length);
            }
        }
    }
}
