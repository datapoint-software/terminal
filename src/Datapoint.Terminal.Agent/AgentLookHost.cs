namespace Datapoint.Terminal.Agent;

// Name is an open terminal or a connection the agent may open; Environment its label, such as production.
public sealed record AgentLookHost(string Name, string Reason, string? Environment = null)
{
    public bool IsConnection { get; init; }
}
