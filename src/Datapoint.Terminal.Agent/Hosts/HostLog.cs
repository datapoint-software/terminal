using System.Globalization;
using System.Text;
using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Hosts;

// A host's maintenance log: one Markdown file per entry, named by when it was written, with a short header saying when and by
// whom. Files are only ever added, so a sync service never has two versions of one to reconcile.
public static class HostLog
{
    public const int MaxEntryLength = 2000;

    private const string Fence = "---";

    public static string Write(string folder, DateTimeOffset when, string author, bool withAgent, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Directory.CreateDirectory(folder);
        var stem = when.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(folder, $"{stem}-{Guid.NewGuid().ToString("N")[..4]}.md");
        var content = new StringBuilder()
            .Append(Fence).Append('\n')
            .Append("date: ").Append(when.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)).Append('\n')
            .Append("by: ").Append(OneLine(author)).Append('\n')
            .Append("agent: ").Append(withAgent ? "true" : "false").Append('\n')
            .Append(Fence).Append("\n\n")
            .Append(text.Trim()).Append('\n')
            .ToString();
        TextFiles.Write(path, content, isPrivate: false);
        return path;
    }

    // Newest first. Entries that can't be read are left out rather than failing the whole log.
    public static IReadOnlyList<HostLogEntry> Read(string folder, int max = int.MaxValue)
    {
        try
        {
            return Directory.Exists(folder)
                ? [.. Directory.EnumerateFiles(folder, "*.md")
                    .Select(TryParse)
                    .OfType<HostLogEntry>()
                    .OrderByDescending(entry => entry.When)
                    .ThenByDescending(entry => entry.Path, StringComparer.Ordinal)
                    .Take(max)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static string Describe(IReadOnlyList<HostLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var text = new StringBuilder("## Maintenance log\n\n");
        if (entries.Count == 0)
        {
            return text.Append("No entries yet.\n").ToString();
        }

        foreach (var entry in entries)
        {
            var who = entry.Author.Length == 0 ? string.Empty : entry.WithAgent ? $" ({entry.Author}, with the agent)" : $" ({entry.Author})";
            text.Append("- ").Append(entry.When.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(who).Append(": ")
                .Append(entry.Text.Replace('\n', ' ')).Append('\n');
        }

        return text.ToString();
    }

    public static HostLogEntry? Parse(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var entry = new HostLogEntry(File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path)) : DateTimeOffset.MinValue, string.Empty, WithAgent: false, string.Empty, path);
        var body = 0;
        if (lines.Length > 0 && string.Equals(lines[0].Trim(), Fence, StringComparison.Ordinal))
        {
            var end = Array.FindIndex(lines, 1, line => string.Equals(line.Trim(), Fence, StringComparison.Ordinal));
            if (end < 0)
            {
                return null;
            }

            entry = lines[1..end].Aggregate(entry, ReadHeader);
            body = end + 1;
        }

        var text = string.Join('\n', lines[body..]).Trim();
        return text.Length == 0 ? null : entry with { Text = text };
    }

    private static HostLogEntry ReadHeader(HostLogEntry entry, string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return entry;
        }

        var value = line[(colon + 1)..].Trim();
        return line[..colon].Trim() switch
        {
            "date" when DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date) => entry with { When = date },
            "by" => entry with { Author = value },
            "agent" => entry with { WithAgent = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) },
            _ => entry,
        };
    }

    private static HostLogEntry? TryParse(string path)
    {
        try
        {
            return Parse(path, TextFiles.Read(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
