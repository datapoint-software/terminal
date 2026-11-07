using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Chat;

// One host on a request to look before planning: which, and what the agent will look for there and why.
public sealed class LookHostViewModel(AgentLookHost host)
{
    public HostChipViewModel Chip { get; } = new(host.Name, host.Environment);

    public string Reason => host.Reason;

    // A connection that isn't open yet; allowing the look also lets the agent open it.
    public bool OpensConnection => host.IsConnection;
}
