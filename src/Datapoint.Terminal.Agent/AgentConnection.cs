namespace Datapoint.Terminal.Agent;

// A place the agent can open a terminal for, with the approval mode a terminal there starts in.
// Place is what its shell runs in when that isn't a machine, e.g. "Docker container". Labels are the host's own, e.g. "web".
public sealed record AgentConnection(string Name, string? Environment, ApprovalMode Mode, string? Place = null)
{
    public IReadOnlyList<string> Labels { get; init => field = value ?? []; } = [];

    // A shell on the user's own machine rather than one of the workspace's hosts, so no skill runs there.
    public bool IsLocalShell { get; init; }
}
