namespace Datapoint.Terminal.Agent;

// Product decision 46, several hosts at once: approving a command in one terminal approves the identical command in the
// workspace's other terminals for the rest of the task, so a rollout asks once. Only between terminals with the same environment
// label, so approving on staging never approves production, and never for commands that always ask.
public sealed partial class TerminalAgent
{
    private readonly List<(string Command, string? Environment, string Terminal)> _approvals = [];

    private CommandApproval? CarriedApproval(AgentTerminal terminal, string command)
    {
        if (ApprovedElsewhere(terminal, command) is not { } first)
        {
            return null;
        }

        Raise(new AgentEventArgs(AgentEventKind.Notice, $"Approved in {first} earlier in this task; running the same command in {terminal.Name}.") { Terminal = terminal.Name });
        return CommandApproval.Approve(command);
    }

    private string? ApprovedElsewhere(AgentTerminal terminal, string command)
    {
        lock (_approvals)
        {
            return _approvals.FirstOrDefault(item =>
                string.Equals(item.Command, command, StringComparison.Ordinal)
                && string.Equals(item.Environment, terminal.Environment, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Terminal, terminal.Name, StringComparison.OrdinalIgnoreCase)).Terminal;
        }
    }

    // The command as the user approved it, edits included.
    private void RememberApproval(AgentTerminal terminal, CommandApproval approval, bool canCarry)
    {
        if (!approval.Approved || !canCarry)
        {
            return;
        }

        lock (_approvals)
        {
            _approvals.Add((approval.Command.Trim(), terminal.Environment, terminal.Name));
        }
    }

    private void ForgetApprovals()
    {
        lock (_approvals)
        {
            _approvals.Clear();
        }
    }

    private List<string> SameEnvironment(AgentTerminal terminal) =>
        [.. Terminals.Where(other => !ReferenceEquals(other, terminal) && string.Equals(other.Environment, terminal.Environment, StringComparison.OrdinalIgnoreCase)).Select(other => other.Name)];
}
