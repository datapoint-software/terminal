using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent;

public sealed record CommandProposal(string Command, string Purpose, CommandRisk Risk, string Terminal, string? Environment = null)
{
    public ProposalKind Kind { get; init; } = ProposalKind.RunCommand;

    // A file listed in .terminalignore that the command names, which is why it asks even in Auto.
    public string? IgnoredFile { get; init; }
}
