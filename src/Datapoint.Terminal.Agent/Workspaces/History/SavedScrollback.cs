namespace Datapoint.Terminal.Agent.Workspaces.History;

// Time is when the terminal closed.
public sealed record SavedScrollback(DateTimeOffset Time, string Text);
