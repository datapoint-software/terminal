using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent;

// Product decision 111: before planning a change, the agent asks in plain words to look at the hosts involved. Allowing it
// lets read-only commands on those hosts run without asking, and their terminals open without asking, until the task ends;
// anything that may change a system asks as the approval mode says. Every command still runs visibly in its terminal.
public sealed partial class TerminalAgent
{
    public const int MaxLookHosts = 20;

    // Names of terminals and connections the user allowed looking at, for this task.
    private readonly HashSet<string> _lookingAt = new(StringComparer.OrdinalIgnoreCase);

    private List<AITool> LookTools() =>
    [
        new ToolFunction(
            "ask_to_look",
            "Asks the user's permission to look at hosts before you propose a plan for a change. Say for each host, in plain "
            + "words, what you want to find out there and why the plan needs it; don't list commands. Once allowed, read-only "
            + "commands on those hosts run without asking until the task ends, and their terminals open without asking; anything "
            + "that may change a system still asks.",
            """
            {"type":"object","properties":{
              "message":{"type":"string","description":"Your message to the user, shown in the chat above the request: in a few plain sentences, what you're about to check and why the plan needs it, e.g. \"Before I plan the updates I'd like to look at the three nodes: what's pending on b and c, and what would have to move while each one drains.\" Don't also write it as text."},
              "title":{"type":"string","description":"The task in a few words, e.g. \"Drain swarm-b\"."},
              "hosts":{"type":"array","description":"Each host to look at.","items":{"type":"object","properties":{
                "host":{"type":"string","description":"An open terminal or a connection, as listed in the user's message."},
                "reason":{"type":"string","description":"What you'll look for there and why the plan needs it, in one or two plain sentences, e.g. \"Which services run on b and whether any keep data on its local disks, since those can't move when it drains.\""}},
               "required":["host","reason"]}}},
             "required":["message","title","hosts"]}
            """,
            AskToLookAsync),
    ];

    private async Task<string> AskToLookAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (ReadLook(arguments, out var problem) is not { } look)
        {
            return problem;
        }

        SayFirst(ToolArguments.GetString(arguments, "message"));
        if (Mode != ApprovalMode.IgnorePermissions)
        {
            SetStatus(AgentStatus.WaitingForApproval);
            var proposal = new CommandProposal(look.Text, look.Title, CommandRisk.ReadOnly, string.Empty) { Kind = ProposalKind.Look, Look = look };
            var approval = await RequestApprovalAsync(proposal, cancellationToken).ConfigureAwait(false);
            SetStatus(AgentStatus.Thinking);
            if (!approval.Approved)
            {
                return "Not allowed." + (string.IsNullOrWhiteSpace(approval.Feedback) ? " Ask the user how they'd like you to go on." : " The user said: " + approval.Feedback);
            }
        }

        lock (_lookingAt)
        {
            _lookingAt.UnionWith(look.Hosts.Select(host => host.Name));
        }

        var toOpen = look.Hosts.Where(host => host.IsConnection).Select(host => host.Name).ToList();
        return "Allowed. Read-only commands on " + string.Join(", ", look.Hosts.Select(host => host.Name)) + " run without asking until the task ends."
            + (toOpen.Count == 0 ? string.Empty : " Open " + string.Join(", ", toOpen) + " with open_terminal first.")
            + " Run one simple command at a time, then propose your plan with what you found.";
    }

    // A card says what's asked; a message in the agent's own words comes first: why it wants to look, or what it found
    // before its plan. It reads as a reply, unless the agent already wrote one in this step.
    private void SayFirst(string? text)
    {
        if (_hasRepliedThisStep || text?.Trim() is not { Length: > 0 } message)
        {
            return;
        }

        _hasRepliedThisStep = true;
        Raise(new AgentEventArgs(AgentEventKind.Reply, message));
    }

    private AgentLook? ReadLook(AIFunctionArguments arguments, out string problem)
    {
        var hosts = new List<AgentLookHost>();
        foreach (var (name, reason) in LookHosts.Parse(arguments))
        {
            if (FindLookHost(name, reason) is not { } host)
            {
                problem = $"Not asked: \"{name}\" is neither an open terminal nor a listed connection.";
                return null;
            }

            hosts.Add(host);
        }

        problem = hosts.Count == 0 ? "Not asked: name at least one host, with what you'll look for there and why."
            : hosts.Any(host => host.Reason.Length == 0) ? "Not asked: say for each host what you'll look for there and why."
            : hosts.Count > MaxLookHosts ? $"Not asked: name at most {MaxLookHosts} hosts."
            : string.Empty;
        var title = ToolArguments.GetString(arguments, "title")?.Trim() is { Length: > 0 } text ? text : "Look before planning";
        return problem.Length == 0 ? new AgentLook(title, [.. hosts.DistinctBy(host => host.Name, StringComparer.OrdinalIgnoreCase)]) : null;
    }

    private AgentLookHost? FindLookHost(string name, string reason)
    {
        if (Terminals.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) is { } terminal)
        {
            return new AgentLookHost(terminal.Name, reason, terminal.Environment);
        }

        return (TerminalOpener?.Connections ?? []).FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) is { } connection
            ? new AgentLookHost(connection.Name, reason, connection.Environment) { IsConnection = true }
            : null;
    }

    // Read-only commands only: the classifier's ReadOnly, never one it can't place.
    private bool MayLook(AgentTerminal terminal, CommandRisk risk)
    {
        if (risk != CommandRisk.ReadOnly)
        {
            return false;
        }

        lock (_lookingAt)
        {
            return _lookingAt.Contains(terminal.Name) || (terminal.Connection is { } connection && _lookingAt.Contains(connection));
        }
    }

    private bool MayOpenToLook(string connection)
    {
        lock (_lookingAt)
        {
            return _lookingAt.Contains(connection);
        }
    }

    private void ForgetLooking()
    {
        lock (_lookingAt)
        {
            _lookingAt.Clear();
        }
    }
}
