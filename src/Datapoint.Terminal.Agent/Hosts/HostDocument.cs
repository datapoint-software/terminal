using System.Globalization;
using System.Text;
using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Hosts;

// A host's README.md (product.md, Host documentation): Markdown with "## " sections, a standard set to start from, which the
// user and the agent can add to. The agent replaces one section at a time, so what the user wrote elsewhere in the file is never
// touched; the maintenance log is kept beside it, one file per entry (HostLog).
public static class HostDocument
{
    public const string LogSection = "Maintenance log";

    // Large enough for a thorough survey; past it, the agent is asked to summarize.
    public const int MaxBytes = 64 * 1024;

    private const string SectionMark = "## ";

    public static IReadOnlyList<string> StandardSections { get; } = ["Overview", "System", "Software and services", "Notes"];

    public static string Template(string connection) =>
        $"# {connection}\n\nDocumentation for {connection}, kept up to date by the agent in Datapoint Terminal and by you. "
        + "Edit it freely: the agent changes one section at a time, and keeps the maintenance log in log/, one file per entry.\n\n"
        + string.Concat(StandardSections.Select(section => SectionMark + section + "\n\n"));

    // Names the host's folder after it, keeping it inside hosts/ whatever the name holds.
    public static string FolderNameFor(string connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var name = new StringBuilder();
        foreach (var character in connection.Trim())
        {
            name.Append(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-');
        }

        var text = name.ToString().Trim('.', '-');
        return text.Length == 0 ? "host" : text;
    }

    public static IReadOnlyList<string> Sections(string text) =>
        [.. Lines(text).Where(IsSection).Select(line => line[SectionMark.Length..].Trim())];

    // The section's text, without its heading; null when there's no such section.
    public static string? Section(string text, string section)
    {
        var lines = Lines(text);
        var (start, end) = Find(lines, section);
        return start < 0 ? null : string.Join('\n', lines[(start + 1)..end]).Trim('\n');
    }

    public static (string Text, WriteResult Result) SetSection(string text, string connection, string section, string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var lines = Lines(text.Length == 0 ? Template(connection) : text);
        var (start, end) = Find(lines, section);
        var replacement = new List<string> { SectionMark + section, string.Empty };
        replacement.AddRange(Lines(body.Trim('\n', '\r')));
        replacement.Add(string.Empty);
        if (start < 0)
        {
            // New sections go before the log, which stays last.
            var (log, _) = Find(lines, LogSection);
            start = end = log < 0 ? lines.Count : log;
        }

        if (lines[start..end].SequenceEqual(replacement, StringComparer.Ordinal))
        {
            return (text, WriteResult.Unchanged);
        }

        lines.RemoveRange(start, end - start);
        lines.InsertRange(start, replacement);
        return Checked(Join(lines));
    }

    // A project's single-file document kept its log as a section of "- yyyy-MM-dd: text" lines; they become entries of their own.
    public static (string Text, IReadOnlyList<(DateOnly Date, string Entry)> Log) SplitLegacyLog(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = Lines(text);
        var (start, end) = Find(lines, LogSection);
        if (start < 0)
        {
            return (text, []);
        }

        var entries = new List<(DateOnly, string)>();
        foreach (var line in lines[(start + 1)..end].Select(line => line.Trim()).Where(line => line.StartsWith("- ", StringComparison.Ordinal)))
        {
            var colon = line.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 2 && DateOnly.TryParseExact(line[2..colon], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                entries.Add((date, line[(colon + 2)..]));
                continue;
            }

            entries.Add((DateOnly.MinValue, line[2..]));
        }

        lines.RemoveRange(start, end - start);
        return (Join(lines), entries);
    }

    private static (string, WriteResult) Checked(string text) =>
        Encoding.UTF8.GetByteCount(text) > MaxBytes ? (text, WriteResult.Full) : (text, WriteResult.Changed);

    // From the heading's line to the next heading, or the end.
    private static (int Start, int End) Find(List<string> lines, string section)
    {
        var start = lines.FindIndex(line => IsSection(line)
            && string.Equals(line[SectionMark.Length..].Trim(), section.Trim(), StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            return (-1, -1);
        }

        var next = lines.FindIndex(start + 1, IsSection);
        return (start, next < 0 ? lines.Count : next);
    }

    private static bool IsSection(string line) => line.StartsWith(SectionMark, StringComparison.Ordinal);

    private static List<string> Lines(string text) => [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];

    private static string Join(List<string> lines) => string.Join('\n', lines).TrimEnd('\n') + "\n";
}
