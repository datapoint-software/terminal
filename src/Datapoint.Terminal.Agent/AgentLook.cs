namespace Datapoint.Terminal.Agent;

// The agent's request to look around before planning a change (product decision 111): what it wants to learn on each host,
// and why, in the agent's words. Allowing it lets read-only commands on those hosts run without asking until the task ends.
public sealed record AgentLook(string Title, IReadOnlyList<AgentLookHost> Hosts)
{
    public string Text => string.Join('\n', Hosts.Select(host => $"{host.Name}: {host.Reason}"));
}
