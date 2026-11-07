using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decisions 45 and 82: before changing a system the agent shows its plan, and once the plan is approved, a step it
// doesn't include stops for the user, even in Auto. The plan lasts for the task.
public sealed partial class TerminalAgent
{
    public const int MaxPlanSteps = 30;

    private AgentPlan? _plan;

    public AgentPlan? Plan => _plan;

    private List<AITool> PlanTools() =>
    [
        new ToolFunction(
            "propose_plan",
            "Shows the user your plan for a task that changes a system, for approval: the steps in order, each with the command "
            + "you intend to run, and how to roll back. Required before changing anything in a terminal in Auto mode. Once it's "
            + "approved, give the step's number as plan_step when you run each command; anything the plan doesn't include asks "
            + "the user first. Propose a new plan if the work changes.",
            """
            {"type":"object","properties":{
              "terminal":{"type":"string","description":"The terminal the plan is for, as listed in the user's message."},
              "summary":{"type":"string","description":"What the plan achieves, in one sentence."},
              "steps":{"type":"array","description":"The steps in order.","items":{"type":"object","properties":{
                "command":{"type":"string","description":"The exact command you'll run, e.g. \"sudo systemctl restart nginx\"."},
                "purpose":{"type":"string","description":"What the step does, in a short plain sentence, e.g. \"Load the new configuration\". Add when you'll stop and ask."},
                "warning":{"type":"string","description":"Only for a step that's risky or hard to undo: what could go wrong, in a few words, e.g. \"Ends the SSH session\"."}},
               "required":["command","purpose"]}},
              "rollback":{"type":"string","description":"How to undo the changes if something goes wrong, in short Markdown."}},
             "required":["summary","steps","rollback"]}
            """,
            ProposePlanAsync),
    ];

    private async Task<string> ProposePlanAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (ReadPlan(arguments, out var problem) is not { } plan)
        {
            return problem;
        }

        var summary = plan.Summary;
        var terminal = FindTerminal(arguments, out _);
        SetStatus(AgentStatus.WaitingForApproval, terminal);
        var proposal = new CommandProposal(plan.Text, summary, CommandRisk.Changes, terminal?.Name ?? string.Empty, terminal?.Environment) { Kind = ProposalKind.Plan, Plan = plan };
        var approval = await _user.RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
        SetStatus(AgentStatus.Thinking);
        if (!approval.Approved)
        {
            return "Not approved." + (string.IsNullOrWhiteSpace(approval.Feedback) ? " Ask the user what they'd prefer." : " The user said: " + approval.Feedback);
        }

        _plan = plan;
        Raise(new AgentEventArgs(AgentEventKind.PlanApproved, summary) { Plan = plan, Terminal = terminal?.Name });
        return "Approved. Give the step's number as plan_step when you run each command.";
    }

    private static AgentPlan? ReadPlan(AIFunctionArguments arguments, out string problem)
    {
        var summary = (ToolArguments.GetString(arguments, "summary") ?? string.Empty).Trim();
        var steps = PlanSteps.Parse(arguments);
        var rollback = (ToolArguments.GetString(arguments, "rollback") ?? string.Empty).Trim();
        problem = summary.Length == 0 || steps.Count == 0 ? "Not proposed: give a summary and at least one step."
            : steps.Any(step => step.Command.Length == 0) ? "Not proposed: give every step the command you'll run."
            : steps.Count > MaxPlanSteps ? $"Not proposed: a plan has at most {MaxPlanSteps} steps. Split the task."
            : string.Empty;
        return problem.Length == 0 ? new AgentPlan(summary, steps, rollback) : null;
    }

    // None when the command may go ahead as the approval mode says; otherwise whether it has to ask, or a refusal in Problem.
    private PlanCheck CheckPlan(AgentTerminal terminal, CommandRisk risk, int? step)
    {
        if (risk == CommandRisk.ReadOnly)
        {
            return PlanCheck.None;
        }

        if (_plan is null)
        {
            return terminal.Mode == ApprovalMode.Auto
                ? new PlanCheck("in Auto mode, show your plan with propose_plan before changing anything.", IsOutside: false, Step: null)
                : PlanCheck.None;
        }

        return step is { } number && number >= 1 && number <= _plan.Steps.Count
            ? new PlanCheck(null, IsOutside: false, number)
            : new PlanCheck(null, IsOutside: true, Step: null);
    }

    private sealed record PlanCheck(string? Problem, bool IsOutside, int? Step)
    {
        public static PlanCheck None { get; } = new(null, IsOutside: false, Step: null);
    }

    private static int? PlanStep(AIFunctionArguments arguments) =>
        ToolArguments.GetString(arguments, "plan_step") is { } value && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var step) ? step : null;
}
