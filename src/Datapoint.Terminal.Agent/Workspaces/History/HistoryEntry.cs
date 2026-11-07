namespace Datapoint.Terminal.Agent.Workspaces.History;

// For a command, Text is the command line and Detail its output. Connection names the connection the terminal was opened for,
// so its entries can be cleared together.
public sealed record HistoryEntry(
    long Conversation,
    DateTimeOffset Time,
    HistoryEntryKind Kind,
    string Text,
    string? Terminal = null,
    string? Detail = null,
    int? ExitCode = null,
    string? Connection = null);
