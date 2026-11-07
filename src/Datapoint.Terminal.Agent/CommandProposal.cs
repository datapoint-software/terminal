using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent;

public sealed record CommandProposal(string Command, string Purpose, CommandRisk Risk, string Terminal, string? Environment = null)
{
    public ProposalKind Kind { get; init; } = ProposalKind.RunCommand;

    // A file listed in .terminalignore that the command names, which is why it asks even in Auto.
    public string? IgnoredFile { get; init; }

    // The approved plan's step the agent says this carries out, or IsOutsidePlan when there's a plan and this isn't in it.
    public int? PlanStep { get; init; }

    public bool IsOutsidePlan { get; init; }
}
