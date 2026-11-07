namespace Datapoint.Terminal.Agent.Projects.History;

// Time is when the terminal closed.
public sealed record SavedScrollback(DateTimeOffset Time, string Text);
