using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Markdown;

internal static class SyntaxHighlighter
{
    // Longer code is shown uncoloured, so a huge reply can't stall the window.
    public const int MaxLength = 50_000;

    public static IReadOnlyList<(string Text, SyntaxKind Kind)> Highlight(string code, string? info)
    {
        var name = info?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (code.Length > MaxLength)
        {
            return [(code, SyntaxKind.Plain)];
        }

        if (name is "diff" or "patch")
        {
            return Diff(code);
        }

        if (SyntaxLanguage.Find(name) is not { } language)
        {
            return [(code, SyntaxKind.Plain)];
        }

        try
        {
            return Tokens(code, language.Pattern);
        }
        catch (RegexMatchTimeoutException)
        {
            return [(code, SyntaxKind.Plain)];
        }
    }

    private static List<(string Text, SyntaxKind Kind)> Tokens(string code, Regex pattern)
    {
        var tokens = new List<(string Text, SyntaxKind Kind)>();
        var position = 0;
        foreach (Match match in pattern.Matches(code))
        {
            if (match.Length == 0)
            {
                continue;
            }

            if (match.Index > position)
            {
                tokens.Add((code[position..match.Index], SyntaxKind.Plain));
            }

            tokens.Add((match.Value, KindOf(match)));
            position = match.Index + match.Length;
        }

        if (position < code.Length)
        {
            tokens.Add((code[position..], SyntaxKind.Plain));
        }

        return tokens;
    }

    private static SyntaxKind KindOf(Match match) =>
        match.Groups["comment"].Success ? SyntaxKind.Comment
        : match.Groups["string"].Success ? SyntaxKind.String
        : match.Groups["variable"].Success ? SyntaxKind.Variable
        : match.Groups["number"].Success ? SyntaxKind.Number
        : SyntaxKind.Keyword;

    private static List<(string Text, SyntaxKind Kind)> Diff(string code)
    {
        var lines = code.Split('\n');
        return [.. lines.Select((line, index) => (index < lines.Length - 1 ? line + "\n" : line, DiffKind(line)))];
    }

    private static SyntaxKind DiffKind(string line) => line switch
    {
        _ when line.StartsWith("+++", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal) => SyntaxKind.Keyword,
        _ when line.StartsWith("@@", StringComparison.Ordinal) => SyntaxKind.Variable,
        _ when line.StartsWith('+') => SyntaxKind.Added,
        _ when line.StartsWith('-') => SyntaxKind.Removed,
        _ => SyntaxKind.Plain,
    };
}
