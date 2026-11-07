using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent;

public sealed record CommandProposal(string Command, string Purpose, CommandRisk Risk, string Terminal, string? Environment = null)
{
    public ProposalKind Kind { get; init; } = ProposalKind.RunCommand;

    // For a plan, its steps and rollback; Command then holds the same plan as text.
    public AgentPlan? Plan { get; init; }

    // A session opened inside the terminal, e.g. "ssh session to web1", where the command will actually run.
    public string? Session { get; init; }

    // A file listed in .terminalignore that the command names, which is why it asks even in Auto.
    public string? IgnoredFile { get; init; }

    // The approved plan's step the agent says this carries out, or IsOutsidePlan when there's a plan and this isn't in it.
    public int? PlanStep { get; init; }

    public bool IsOutsidePlan { get; init; }

    // The other terminals where approving this also approves the identical command until the task ends.
    public IReadOnlyList<string> AlsoApproves { get; init; } = [];
}
