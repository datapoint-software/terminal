namespace Datapoint.Terminal.Agent.Projects;

public sealed record ConnectionChange(string Connection, string Setting, string? Confirmed, string? Proposed);
