using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Hosts;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
    // The text of each file as the agent last read or wrote it, so a section is never replaced over an edit it hasn't seen.
    private readonly Dictionary<string, string> _hostDocsSeen = new(StringComparer.Ordinal);

    public HostDocsFolder? HostDocs { get; set; }

    public TimeProvider Clock { get; set; } = TimeProvider.System;

    // The newest entries are what the agent needs to know what changed lately; older ones stay in the files.
    private const int MaxLogEntriesRead = 20;

    private List<AITool> HostDocsTools() =>
    [
        new ToolFunction(
            "read_host_docs",
            "Returns a host's documentation from the workspace's hosts/ folder: what the machine is, what runs on it, and its "
            + "latest maintenance log entries. Read it before working on a host, and before changing it.",
            """
            {"type":"object","properties":{
              "connection":{"type":"string","description":"The connection's name, as listed in the user's message."}},
             "required":["connection"]}
            """,
            ReadHostDocsAsync),
        new ToolFunction(
            "update_host_docs",
            "Replaces one section of a host's documentation, or adds it, creating the file with the standard sections "
            + "(Overview, System, Software and services, Notes) if there's none. Read the file first and keep "
            + "what the user wrote in the section. Write for people: complete sentences or lists, no secrets.",
            """
            {"type":"object","properties":{
              "connection":{"type":"string"},
              "section":{"type":"string","description":"The section's heading, without the ##."},
              "text":{"type":"string","description":"The section's new Markdown, without its heading."}},
             "required":["connection","section","text"]}
            """,
            UpdateHostDocsAsync),
        new ToolFunction(
            "log_host_change",
            "Adds an entry to a host's maintenance log: what you changed on the machine and why, in one line. The date and "
            + "who did it are added for you. Do it after any task that changed the system.",
            """
            {"type":"object","properties":{
              "connection":{"type":"string"},
              "entry":{"type":"string","description":"One line, e.g. \"Upgraded nginx to 1.26 to fix the TLS warning.\""}},
             "required":["connection","entry"]}
            """,
            LogHostChangeAsync),
    ];

    private string DescribeHostDocs()
    {
        if (HostDocs is not { } docs)
        {
            return string.Empty;
        }

        var documented = docs.Documented();
        return documented.Count == 0
            ? "[No host documentation yet in hosts/.]\n"
            : "[Host documentation in hosts/ for: " + string.Join(", ", documented.Select(name => $"\"{name}\"")) + "]\n";
    }

    private Task<string> ReadHostDocsAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (HostDocsFile(arguments, out var connection) is not { } path)
        {
            return Task.FromResult(connection);
        }

        var text = TryRead(path);
        if (text is null)
        {
            return Task.FromResult("Not read: the file couldn't be opened.");
        }

        Remember(path, text);
        var log = HostLog.Read(HostDocs!.LogFor(connection), MaxLogEntriesRead).Where(entry => !HostDocs.IsIgnored(entry.Path)).ToList();
        var document = text.Length == 0 ? $"There's no documentation for \"{connection}\" yet. Create it with update_host_docs." : text.TrimEnd('\n');
        return Task.FromResult(_redactor.Redact(document + "\n\n" + HostLog.Describe(log)));
    }

    private Task<string> UpdateHostDocsAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var section = (ToolArguments.GetString(arguments, "section") ?? string.Empty).Trim().TrimStart('#').Trim();
        var body = ToolArguments.GetString(arguments, "text") ?? string.Empty;
        if (section.Length == 0 || section.Contains('\n', StringComparison.Ordinal))
        {
            return Task.FromResult("Not changed: give the section's heading on one line.");
        }

        if (string.Equals(section, HostDocument.LogSection, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("Not changed: add to the maintenance log with log_host_change; earlier entries stay as they are.");
        }

        return Task.FromResult(WriteHostDocs(arguments, body, requireSeen: true, (text, connection) => HostDocument.SetSection(text, connection, section, body), section));
    }

    private Task<string> LogHostChangeAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var entry = string.Join(' ', (ToolArguments.GetString(arguments, "entry") ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (entry.Length == 0)
        {
            return Task.FromResult("Not logged: the entry is empty.");
        }

        if (entry.Length > HostLog.MaxEntryLength)
        {
            return Task.FromResult($"Not logged: keep the entry under {HostLog.MaxEntryLength} characters.");
        }

        if (HostDocsFile(arguments, out var connection) is not { } path)
        {
            return Task.FromResult(connection);
        }

        if (!HostDocs!.CanWrite)
        {
            return Task.FromResult("Not changed: this window can't write the workspace's files (it's open read-only).");
        }

        if (_redactor.Find(entry).Count > 0)
        {
            return Task.FromResult("Not changed: the text looks like it holds a secret. Describe where the secret is kept instead, never its value.");
        }

        try
        {
            HostLog.Write(HostDocs.LogFor(connection), Clock.GetLocalNow(), HostDocs.Author, withAgent: true, entry);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult("Not logged: the entry couldn't be written.");
        }

        Raise(new AgentEventArgs(AgentEventKind.HostDocsChanged, Say("NoticeHostDocsLogged", connection)) { Path = path });
        return Task.FromResult("Done.");
    }

    private string WriteHostDocs(
        AIFunctionArguments arguments,
        string written,
        bool requireSeen,
        Func<string, string, (string Text, WriteResult Result)> change,
        string section)
    {
        if (HostDocsFile(arguments, out var connection) is not { } path)
        {
            return connection;
        }

        if (!HostDocs!.CanWrite)
        {
            return "Not changed: this window can't write the workspace's files (it's open read-only).";
        }

        if (_redactor.Find(written).Count > 0)
        {
            return "Not changed: the text looks like it holds a secret. Describe where the secret is kept instead, never its value.";
        }

        return Apply(path, connection, requireSeen, change, section);
    }

    private string Apply(string path, string connection, bool requireSeen, Func<string, string, (string Text, WriteResult Result)> change, string section)
    {
        lock (_hostDocsSeen)
        {
            var current = TryRead(path);
            if (current is null)
            {
                return "Not changed: the file couldn't be opened.";
            }

            if (requireSeen && current.Length > 0 && (!_hostDocsSeen.TryGetValue(path, out var seen) || !string.Equals(seen, current, StringComparison.Ordinal)))
            {
                return "Not changed: the file changed since you last read it, perhaps edited by the user. Read it again, then keep their changes.";
            }

            var (text, result) = change(current, connection);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (result == WriteResult.Full)
            {
                return $"Not changed: the documentation would pass {HostDocument.MaxBytes / 1024} KB. Summarize older or less useful parts first.";
            }

            if (result == WriteResult.Unchanged)
            {
                return "Already up to date.";
            }

            try
            {
                TextFiles.Write(path, text, isPrivate: false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return "Not changed: the file couldn't be written.";
            }

            _hostDocsSeen[path] = text;
            Raise(new AgentEventArgs(AgentEventKind.HostDocsChanged, Say("NoticeHostDocsUpdated", section, connection)) { Path = path });
            return "Done.";
        }
    }

    // The file for a connection the agent knows of, or null with the reason in connection.
    private string? HostDocsFile(AIFunctionArguments arguments, out string connection)
    {
        connection = (ToolArguments.GetString(arguments, "connection") ?? string.Empty).Trim();
        if (HostDocs is not { } docs)
        {
            connection = "There's no host documentation in this window.";
            return null;
        }

        var name = connection;
        var known = Terminals.Select(terminal => terminal.Name)
            .Concat(TerminalOpener?.Connections.Select(item => item.Name) ?? [])
            .Concat(docs.Documented())
            .FirstOrDefault(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
        if (known is null)
        {
            connection = $"There's no connection called \"{name}\". Use one listed in the user's message.";
            return null;
        }

        connection = known;
        var path = docs.FileFor(known);
        if (docs.IsIgnored(path))
        {
            connection = $"The documentation for \"{known}\" is listed in .terminalignore, so you can't read or change it.";
            return null;
        }

        return path;
    }

    private void Remember(string path, string text)
    {
        lock (_hostDocsSeen)
        {
            _hostDocsSeen[path] = text;
        }
    }

    private static string? TryRead(string path)
    {
        try
        {
            return TextFiles.Read(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
