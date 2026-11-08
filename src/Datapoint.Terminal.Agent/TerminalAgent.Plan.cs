using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decisions 45 and 82: before changing a system the agent shows its plan, and once the plan is approved, a step it
// doesn't include stops for the user, even in Plan mode (not when the user ignores permissions). The plan lasts for the task.
public sealed partial class TerminalAgent
{
    public const int MaxPlanSteps = 30;

    private AgentPlan? _plan;

    // The last plan proposed in this task, approved or not, so a revision can show what changed.
    private AgentPlan? _proposedPlan;

    public AgentPlan? Plan => _plan;

    // The commands the user taught to treat as read-only, from their own settings (product decision 112). Replaced whole, so
    // the agent's thread always reads a complete list.
    public LearnedCommands LearnedCommands { get; set; } = LearnedCommands.Empty;

    private List<AITool> PlanTools() =>
    [
        new ToolFunction(
            "propose_plan",
            "Shows the user your plan for a task that changes a system, for approval: what you found, the steps in order, and how "
            + "to roll back. Look first (ask_to_look) when the plan depends on the hosts' state. Each step is one action, names the "
            + "hosts it runs on, says why it's needed, and gives the command, one simple command per line; mark it as an example "
            + "when the exact command depends on what you'll see then. Required before changing anything in Plan mode. Once it's "
            + "approved, give the step's number as plan_step when you run each command; anything the plan doesn't include asks "
            + "the user first. The user may comment instead of approving: then propose a revised plan that answers each comment.",
            """
            {"type":"object","properties":{
              "terminal":{"type":"string","description":"The terminal the plan is mostly about, as listed in the user's message."},
              "title":{"type":"string","description":"The task in a few words, e.g. \"Drain swarm-b for a kernel update\"."},
              "summary":{"type":"string","description":"What the plan achieves, in one sentence."},
              "findings":{"type":"string","description":"What you found while looking that the plan rests on, as a short Markdown list naming the hosts. It's your message to the user in the chat before the plan, and heads the plan; don't also write it as text."},
              "steps":{"type":"array","description":"The steps in order.","items":{"type":"object","properties":{
                "title":{"type":"string","description":"The action in a few words, e.g. \"Move the Postgres leader off swarm-b\"."},
                "terminals":{"type":"array","items":{"type":"string"},"description":"The hosts the step runs on: open terminals or listed connections. Never write them into the other fields."},
                "command":{"type":"string","description":"The command you'll run; several short commands go on separate lines, never joined with ; or loops. E.g. \"sudo systemctl restart nginx\"."},
                "example":{"type":"boolean","description":"True when the command illustrates the step rather than being exactly what you'll run."},
                "purpose":{"type":"string","description":"Why the step is needed, in a short plain sentence, e.g. \"Loads the new configuration\"."},
                "warning":{"type":"string","description":"Only for a step that's risky or hard to undo: what could go wrong, in a few words, e.g. \"Ends the SSH session\"."}},
               "required":["title","terminals","command","purpose"]}},
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
        SayFirst(plan.Findings);
        SetStatus(AgentStatus.WaitingForApproval, terminal);
        var proposal = new CommandProposal(plan.Text, summary, CommandRisk.Changes, terminal?.Name ?? string.Empty, terminal?.Environment) { Kind = ProposalKind.Plan, Plan = plan, Session = terminal?.Terminal.GetState().Session, Environments = EnvironmentsOf(plan), PreviousPlan = _proposedPlan };
        _proposedPlan = plan;
        var approval = await RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
        SetStatus(AgentStatus.Thinking);
        if (!approval.Approved)
        {
            return "Not approved." + (string.IsNullOrWhiteSpace(approval.Feedback) ? " Ask the user what they'd prefer." : " The user said: " + approval.Feedback);
        }

        _plan = plan;
        Raise(new AgentEventArgs(AgentEventKind.PlanApproved, summary) { Plan = plan, Terminal = terminal?.Name });
        return "Approved. Give the step's number as plan_step when you run each command.";
    }

    private AgentPlan? ReadPlan(AIFunctionArguments arguments, out string problem)
    {
        var summary = (ToolArguments.GetString(arguments, "summary") ?? string.Empty).Trim();
        var steps = PlanSteps.Parse(arguments);
        if (UnknownHost(steps) is { } unknown)
        {
            problem = $"Not proposed: \"{unknown}\" is neither an open terminal nor a listed connection.";
            return null;
        }

        var rollback = (ToolArguments.GetString(arguments, "rollback") ?? string.Empty).Trim();
        problem = PlanProblem(summary, steps);
        return problem.Length == 0
            ? new AgentPlan(summary, steps, rollback) { Title = Optional(arguments, "title"), Findings = Optional(arguments, "findings") }
            : null;
    }

    private static string PlanProblem(string summary, IReadOnlyList<AgentPlanStep> steps) =>
        summary.Length == 0 || steps.Count == 0 ? "Not proposed: give a summary and at least one step."
            : steps.Any(step => step.Command.Length == 0) ? "Not proposed: give every step the command you'll run, or an example of it."
            : steps.Count > MaxPlanSteps ? $"Not proposed: a plan has at most {MaxPlanSteps} steps. Split the task."
            : string.Empty;

    private static string? Optional(AIFunctionArguments arguments, string name) =>
        ToolArguments.GetString(arguments, name)?.Trim() is { Length: > 0 } text ? text : null;

    private Dictionary<string, string> EnvironmentsOf(AgentPlan plan)
    {
        var environments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in plan.Steps.SelectMany(step => step.Terminals).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (EnvironmentOf(name) is not { } environment)
            {
                continue;
            }

            environments[name] = environment;
        }

        return environments;
    }

    private string? EnvironmentOf(string name) =>
        Terminals.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(item.Connection, name, StringComparison.OrdinalIgnoreCase))?.Environment
        ?? (TerminalOpener?.Connections ?? []).FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Environment;

    private string? UnknownHost(IReadOnlyList<AgentPlanStep> steps)
    {
        var known = Terminals.SelectMany(terminal => new[] { terminal.Name, terminal.Connection })
            .Concat((TerminalOpener?.Connections ?? []).Select(connection => connection.Name))
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return steps.SelectMany(step => step.Terminals).FirstOrDefault(name => !known.Contains(name));
    }

    // None when the command may go ahead as the approval mode says; otherwise whether it has to ask, or a refusal in Problem.
    private PlanCheck CheckPlan(AgentTerminal terminal, CommandRisk risk, int? step)
    {
        if (risk == CommandRisk.ReadOnly)
        {
            return PlanCheck.None;
        }

        if (RolloutProblem(terminal) is { } rollout)
        {
            return new PlanCheck(rollout, IsOutside: false, Step: null);
        }

        if (_plan is null)
        {
            return Mode == ApprovalMode.Plan
                ? new PlanCheck("in Plan mode, show your plan with propose_plan before changing anything.", IsOutside: false, Step: null)
                : PlanCheck.None;
        }

        return step is { } number && number >= 1 && number <= _plan.Steps.Count && IsOnStepHost(_plan.Steps[number - 1], terminal)
            ? new PlanCheck(null, IsOutside: false, number)
            : new PlanCheck(null, IsOutside: true, Step: null);
    }

    // A step approved for some hosts doesn't cover the same command on another one.
    private static bool IsOnStepHost(AgentPlanStep step, AgentTerminal terminal) =>
        step.Terminals.Count == 0
        || step.Terminals.Any(name => string.Equals(name, terminal.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, terminal.Connection, StringComparison.OrdinalIgnoreCase));

    private sealed record PlanCheck(string? Problem, bool IsOutside, int? Step)
    {
        public static PlanCheck None { get; } = new(null, IsOutside: false, Step: null);
    }

    private static int? PlanStep(AIFunctionArguments arguments) =>
        ToolArguments.GetString(arguments, "plan_step") is { } value && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var step) ? step : null;
}
