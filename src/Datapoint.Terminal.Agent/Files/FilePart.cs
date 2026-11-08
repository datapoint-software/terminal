using System.Globalization;
using System.Text;

namespace Datapoint.Terminal.Agent.Files;

// Part of a text file for read_file: a range of lines, or the lines containing some text, numbered as `grep -n` and `sed -n`
// number them, so the agent can read only what a task needs from a long file or log (product decision 130).
public sealed record FilePart(int? StartLine, int? LineCount, string? Search)
{
    public const int DefaultLines = 200;
    public const int MostLines = 2_000;
    public const int MostMatches = 200;

    public static string Describe(string text, FilePart part)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(part);
        var lines = text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        return part.Search is { Length: > 0 } search ? Matches(lines, search) : Range(lines, part);
    }

    private static string Range(string[] lines, FilePart part)
    {
        var start = Math.Max(1, part.StartLine ?? 1);
        if (start > lines.Length)
        {
            return string.Create(CultureInfo.InvariantCulture, $"The file has {lines.Length:N0} lines, so there's no line {start:N0}.");
        }

        var count = Math.Clamp(part.LineCount ?? DefaultLines, 1, MostLines);
        var end = Math.Min(lines.Length, start + count - 1);
        var text = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"Lines {start:N0} to {end:N0} of {lines.Length:N0}.\n"));
        for (var number = start; number <= end; number++)
        {
            text.Append(number.ToString(CultureInfo.InvariantCulture)).Append(':').Append(lines[number - 1]).Append('\n');
        }

        return text.ToString();
    }

    private static string Matches(string[] lines, string search)
    {
        var matching = lines.Select((line, index) => (Line: line, Number: index + 1))
            .Where(item => item.Line.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var text = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"{matching.Count:N0} of {lines.Length:N0} lines contain \"{search}\""));
        text.Append(matching.Count > MostMatches ? string.Create(CultureInfo.InvariantCulture, $"; the first {MostMatches} are shown. Search for something narrower to see the rest.\n") : ".\n");
        foreach (var (line, number) in matching.Take(MostMatches))
        {
            text.Append(number.ToString(CultureInfo.InvariantCulture)).Append(':').Append(line).Append('\n');
        }

        return text.ToString();
    }
}
