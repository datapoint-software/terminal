using System.Text;
using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Agent.Projects.Ignore;

// One line of a .gitignore, as gitignore(5) describes it. Patterns come from folders that may not be trusted, so they're run
// without backtracking and with a timeout.
public sealed class IgnorePattern
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromMilliseconds(250);

    private readonly Regex _regex;

    private IgnorePattern(Regex regex, bool isNegated, bool isDirectoryOnly)
    {
        _regex = regex;
        IsNegated = isNegated;
        IsDirectoryOnly = isDirectoryOnly;
    }

    public bool IsNegated { get; }

    public bool IsDirectoryOnly { get; }

    public static IgnorePattern? Parse(string line, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = TrimTrailingSpaces(line);
        if (text.Length == 0 || text[0] == '#')
        {
            return null;
        }

        var isNegated = text[0] == '!';
        text = isNegated ? text[1..] : text;
        var isDirectoryOnly = text.EndsWith('/') && !text.EndsWith("\\/", StringComparison.Ordinal);
        text = isDirectoryOnly ? text.TrimEnd('/') : text;
        if (text.Length == 0)
        {
            return null;
        }

        // A slash at the start or in the middle ties the pattern to the ignore file's folder; otherwise it matches at any depth.
        var isAnchored = text.Contains('/', StringComparison.Ordinal);
        text = text.TrimStart('/');
        var pattern = (isAnchored || text.StartsWith("**", StringComparison.Ordinal) ? "^" : "^(?:.*/)?") + Translate(text) + "$";
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        try
        {
            return new IgnorePattern(new Regex(pattern, options, s_timeout), isNegated, isDirectoryOnly);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // The path is relative to the ignore file's folder, with '/' between names.
    public bool Matches(string relativePath, bool isDirectory)
    {
        if (IsDirectoryOnly && !isDirectory)
        {
            return false;
        }

        try
        {
            return _regex.IsMatch(relativePath);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string TrimTrailingSpaces(string line)
    {
        var end = line.TrimEnd('\r').Length;
        while (end > 0 && line[end - 1] == ' ' && !(end > 1 && line[end - 2] == '\\'))
        {
            end--;
        }

        return line[..end];
    }

    private static string Translate(string glob)
    {
        var regex = new StringBuilder();
        for (var index = 0; index < glob.Length; index++)
        {
            index = AppendToken(glob, index, regex);
        }

        return regex.ToString();
    }

    // Returns the index of the token's last character.
    private static int AppendToken(string glob, int index, StringBuilder regex)
    {
        var character = glob[index];
        switch (character)
        {
            case '*':
                return AppendStars(glob, index, regex);
            case '?':
                regex.Append("[^/]");
                return index;
            case '[':
                return AppendClass(glob, index, regex);
            case '\\' when index + 1 < glob.Length:
                regex.Append(Regex.Escape(glob[index + 1].ToString()));
                return index + 1;
            default:
                regex.Append(Regex.Escape(character.ToString()));
                return index;
        }
    }

    // "**" is special only as a whole name: "**/x" (any folders), "x/**" (everything inside) and "x/**/y" (zero or more folders).
    private static int AppendStars(string glob, int index, StringBuilder regex)
    {
        var end = index;
        while (end + 1 < glob.Length && glob[end + 1] == '*')
        {
            end++;
        }

        var isDouble = end > index;
        var startsName = index == 0 || glob[index - 1] == '/';
        var endsName = end + 1 == glob.Length || glob[end + 1] == '/';
        if (!isDouble || !startsName || !endsName)
        {
            regex.Append("[^/]*");
            return end;
        }

        if (end + 1 == glob.Length)
        {
            regex.Append(".*");
            return end;
        }

        regex.Append("(?:.*/)?");
        return end + 1;
    }

    private static int AppendClass(string glob, int index, StringBuilder regex)
    {
        var close = glob.IndexOf(']', index + 2);
        if (close < 0)
        {
            regex.Append("\\[");
            return index;
        }

        var body = glob[(index + 1)..close];
        var isNegated = body.StartsWith('!') || body.StartsWith('^');
        body = isNegated ? body[1..] : body;
        regex.Append('[').Append(isNegated ? "^/" : string.Empty)
            .Append(body.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal))
            .Append(']');
        return close;
    }
}
