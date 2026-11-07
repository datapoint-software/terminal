using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
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

        if (connection.Mode == ApprovalMode.Ask)
        {
            SetStatus(AgentStatus.WaitingForApproval);
            var proposal = new CommandProposal(connection.Name, purpose, CommandRisk.Changes, connection.Name, connection.Environment) { Kind = ProposalKind.OpenTerminal };
            var approval = await _user.RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
            SetStatus(AgentStatus.Thinking);
            if (!approval.Approved)
            {
                return "Not opened: the user declined." + (string.IsNullOrWhiteSpace(approval.Feedback) ? string.Empty : " They said: " + approval.Feedback);
            }
        }

        Raise(new AgentEventArgs(AgentEventKind.Notice, $"Opening a terminal for {connection.Name}: {purpose}"));
        var terminal = await opener.OpenAsync(connection.Name, cancellationToken).ConfigureAwait(false);
        return terminal is null
            ? "Not opened: the connection failed; the user can see why in the chat."
            : $"Opened terminal \"{terminal}\". Name it in your tool calls.";
    }
}
