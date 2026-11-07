using System.Globalization;
using Datapoint.Terminal.Agent.Hosts;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Editor;

// One entry of a host's maintenance log as the document shows it: when, who, and what was done.
public sealed record HostLogItem(string When, string Who, string Text)
{
    public string AccessibleName => $"{When}, {Who}: {Text}";

    public static HostLogItem From(HostLogEntry entry, string userName)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var author = entry.Author.Length == 0 ? null
            : string.Equals(entry.Author, userName, StringComparison.OrdinalIgnoreCase) ? Strings.DocumentLogYou
            : entry.Author;
        var who = (author, entry.WithAgent) switch
        {
            (null, true) => Strings.DocumentLogAgentOnly,
            (null, false) => string.Empty,
            (_, true) => Strings.Format(Strings.DocumentLogWithAgent, author),
            _ => author,
        };
        var when = entry.When.ToLocalTime().ToString("d MMM yyyy · HH:mm", CultureInfo.CurrentCulture);
        return new HostLogItem(when, who, entry.Text);
    }
}
