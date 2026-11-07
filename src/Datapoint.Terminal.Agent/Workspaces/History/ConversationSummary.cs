namespace Datapoint.Terminal.Agent.Workspaces.History;

// Title is the conversation's first message from the user, or its first entry.
public sealed record ConversationSummary(long Id, DateTimeOffset Started, string Title, int Entries);
