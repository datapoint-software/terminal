using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent;

public sealed record CommandProposal(string Command, string Purpose, CommandRisk Risk, string Terminal, string? Environment = null)
{
    public ProposalKind Kind { get; init; } = ProposalKind.RunCommand;
}
