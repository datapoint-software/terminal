using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decision 114: when the user asks for something that will change servers outside Plan mode, the agent offers once
// per task to plan it first. Accepting switches to Plan mode, as choosing it in the mode picker would, so the agent then
// looks and plans as decisions 45 and 111 say. Only the user's click switches the mode; the agent can't do it itself.
public sealed partial class TerminalAgent
{
    // Set once offered in a task, accepted or not, so the user is asked at most once.
    private volatile bool _offeredPlanMode;

    private List<AITool> PlanModeTools() =>
    [
        new ToolFunction(
            "offer_plan_mode",
            "Asks the user whether to plan a task before doing it, when they've asked for something that will change one or "
            + "more servers and the approval mode isn't Plan. Call it before looking or changing anything, at most once per "
            + "task, and not for a one-off change the user spelled out. If they accept, the mode switches to Plan: look with "
            + "ask_to_look if the plan depends on the hosts' state, then propose_plan. If they decline, go on in the current mode "
            + "and don't offer again for this task.",
            """
            {"type":"object","properties":{
              "title":{"type":"string","description":"The task in a few words, e.g. \"Upgrade nginx on the web servers\"."},
              "hosts":{"type":"array","items":{"type":"string"},"description":"The servers the task will change: open terminals or listed connections."},
              "reason":{"type":"string","description":"Why a plan helps here, in one plain sentence, e.g. \"It restarts nginx on three production servers, one at a time.\""}},
             "required":["title","reason"]}
            """,
            OfferPlanModeAsync),
    ];

    private async Task<string> OfferPlanModeAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var title = ToolArguments.GetString(arguments, "title")?.Trim() ?? string.Empty;
        var reason = ToolArguments.GetString(arguments, "reason")?.Trim() ?? string.Empty;
        if (OfferProblem(title, reason) is { } problem)
        {
            return problem;
        }

        var hosts = ToolArguments.GetStrings(arguments, "hosts").Select(host => host.Trim()).Where(host => host.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _offeredPlanMode = true;
        SetStatus(AgentStatus.WaitingForApproval);
        var proposal = new CommandProposal(title, reason, CommandRisk.Changes, string.Join(", ", hosts)) { Kind = ProposalKind.PlanMode };
        var approval = await RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
        SetStatus(AgentStatus.Thinking);
        if (!approval.Approved)
        {
            return "The user chose not to switch to Plan mode. Go on in the current mode, which still decides what asks first, "
                + "and don't offer again for this task." + (string.IsNullOrWhiteSpace(approval.Feedback) ? string.Empty : " The user said: " + approval.Feedback);
        }

        Mode = ApprovalMode.Plan;
        return "Switched to Plan mode. Look first with ask_to_look if the plan depends on the hosts' state, then propose_plan; "
            + "nothing that changes a system runs before the user approves the plan.";
    }

    private string? OfferProblem(string title, string reason) =>
        Mode == ApprovalMode.Plan ? "Not offered: the approval mode is already Plan. Look if needed, then propose_plan."
            : _offeredPlanMode ? "Not offered: you already offered to plan this task. Go on in the current mode."
            : title.Length == 0 || reason.Length == 0 ? "Not offered: give the task's title and why a plan helps."
            : null;
}
