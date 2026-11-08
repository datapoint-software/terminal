using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
    private List<AITool> OpenTools() =>
    [
        new ToolFunction(
            "open_terminal",
            "Opens a new terminal for one of the connections listed in the user's message, as a new tab, and returns its name. The "
            + "user sees it open and may have to approve it first. Use it when the task needs a machine that has no open terminal.",
            """
            {"type":"object","properties":{
              "connection":{"type":"string","description":"The connection's name, as listed in the user's message."},
              "purpose":{"type":"string","description":"Why you need it, in a short sentence the user reads when approving it."}},
             "required":["connection","purpose"]}
            """,
            OpenTerminalAsync),
    ];

    // Opens terminals for the application's connections; without one, open_terminal reports that it can't.
    public ITerminalOpener? TerminalOpener { get; set; }

    // Returns a file listed in .terminalignore that a command names, so it asks first in every mode; null when it names none.
    public Func<AgentTerminal, string, string?>? IgnoredFileCheck { get; set; }

    // Product.md, The agent: opening a terminal is shown in the chat and, where the connection's terminals start in Ask mode,
    // waits for the user's approval like a command. It runs nothing in the new terminal.
    private async Task<string> OpenTerminalAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var name = ToolArguments.GetString(arguments, "connection")?.Trim();
        var purpose = ToolArguments.GetString(arguments, "purpose") ?? string.Empty;
        if (TerminalOpener is not { } opener)
        {
            return "Not opened: this window can't open terminals for you; ask the user to open one.";
        }

        var connection = opener.Connections.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (connection is null)
        {
            return $"Not opened: there's no connection called \"{name}\". Use one of the connections listed in the user's message.";
        }

        if (Mode == ApprovalMode.Ask && !MayOpenToLook(connection.Name))
        {
            SetStatus(AgentStatus.WaitingForApproval);
            var proposal = new CommandProposal(connection.Name, purpose, CommandRisk.Changes, connection.Name, connection.Environment) { Kind = ProposalKind.OpenTerminal };
            var approval = await RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
            SetStatus(AgentStatus.Thinking);
            if (!approval.Approved)
            {
                return "Not opened: the user declined." + (string.IsNullOrWhiteSpace(approval.Feedback) ? string.Empty : " They said: " + approval.Feedback);
            }
        }

        Raise(new AgentEventArgs(AgentEventKind.Notice, $"Opening a terminal for {connection.Name}: {purpose}") { Terminal = connection.Name });
        var terminal = await opener.OpenAsync(connection.Name, cancellationToken).ConfigureAwait(false);
        return terminal is null
            ? "Not opened: the connection failed; the user can see why in the chat."
            : $"Opened terminal \"{terminal}\". Name it in your tool calls.";
    }
}
