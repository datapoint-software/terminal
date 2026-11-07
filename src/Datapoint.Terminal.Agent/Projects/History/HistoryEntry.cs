namespace Datapoint.Terminal.Agent.Projects.History;

// For a command, Text is the command line and Detail its output.
public sealed record HistoryEntry(
    long Conversation,
    DateTimeOffset Time,
    HistoryEntryKind Kind,
    string Text,
    string? Terminal = null,
    string? Detail = null,
    int? ExitCode = null);
