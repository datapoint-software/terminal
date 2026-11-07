namespace Datapoint.Terminal.Agent;

// A place the agent can open a terminal for, with the approval mode a terminal there starts in.
public sealed record AgentConnection(string Name, string? Environment, ApprovalMode Mode);
