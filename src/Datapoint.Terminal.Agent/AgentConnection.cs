namespace Datapoint.Terminal.Agent;

// A place the agent can open a terminal for, with the approval mode a terminal there starts in.
// Place is what its shell runs in when that isn't a machine, e.g. "Docker container".
public sealed record AgentConnection(string Name, string? Environment, ApprovalMode Mode, string? Place = null);
