namespace Datapoint.Terminal.Agent.Workspaces;

public sealed record ConnectionChange(string Connection, string Setting, string? Confirmed, string? Proposed);
