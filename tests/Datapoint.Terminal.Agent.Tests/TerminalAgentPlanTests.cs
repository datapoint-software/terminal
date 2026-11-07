using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentPlanTests
{
    private static readonly string[] s_steps = ["sudo apt-get install -y nginx: install it", "sudo systemctl enable --now nginx: start it"];

    private readonly FakeUser _user = new();
    private readonly FakeTerminal _terminal = new();
    private readonly List<string?> _results = [];
    private readonly List<AgentEventArgs> _events = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, object?> Arguments(params (string Name, object? Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Plan() =>
        _ => Call("propose_plan", Arguments(("terminal", "web1"), ("summary", "Install nginx"), ("steps", s_steps), ("rollback", "sudo apt-get remove nginx")));

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Run(string command, int? step = null) =>
        _ => Call("run_command", step is null
            ? Arguments(("terminal", "web1"), ("command", command), ("purpose", "install"))
            : Arguments(("terminal", "web1"), ("command", command), ("purpose", "install"), ("plan_step", step)));

    private Func<IReadOnlyList<ChatMessage>, ChatResponse> Record(Func<IReadOnlyList<ChatMessage>, ChatResponse> next) =>
        messages =>
        {
            _results.Add(LastToolResult(messages));
            return next(messages);
        };

    private async Task<TerminalAgent> RunAsync(ApprovalMode mode, params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] steps)
    {
        var script = steps.Select((step, index) => index == 0 ? step : Record(step)).Append(Record(_ => Text("Done."))).ToArray();
        var agent = new TerminalAgent(new ScriptedChatClient(script), _user, new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("web1", _terminal, mode, "production"));
        agent.EventRaised += (_, e) => _events.Add(e);
        await agent.SendAsync("Install nginx", Token);
        return agent;
    }

    [Fact]
    public async Task Shows_the_plan_for_approval_and_notes_it_once_approved()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, Plan());

        var proposal = Assert.Single(_user.Proposals);
        Assert.Equal((ProposalKind.Plan, "Install nginx", "web1", "production"), (proposal.Kind, proposal.Purpose, proposal.Terminal, proposal.Environment));
        Assert.Equal(
            "1. sudo apt-get install -y nginx: install it\n2. sudo systemctl enable --now nginx: start it\n\nTo roll back: sudo apt-get remove nginx",
            proposal.Command);
        Assert.StartsWith("Approved.", _results[0], StringComparison.Ordinal);
        Assert.Contains(_events, e => e.Kind == AgentEventKind.Notice && e.Text.StartsWith("Plan approved: Install nginx", StringComparison.Ordinal));
        Assert.Null(agent.Plan);
    }

    [Fact]
    public async Task Refuses_changes_in_Auto_without_a_plan()
    {
        using var agent = await RunAsync(ApprovalMode.Auto, Run("sudo apt-get install -y nginx"), Run("uptime"));

        Assert.Equal("Not run: in Auto mode, show your plan with propose_plan before changing anything.", _results[0]);
        Assert.Equal(["uptime"], _terminal.Typed);
        Assert.Empty(_user.Proposals);
    }

    [Fact]
    public async Task Runs_planned_steps_in_Auto_without_asking_again()
    {
        using var agent = await RunAsync(ApprovalMode.Auto, Plan(), Run("sudo apt-get install -y nginx", step: 1));

        Assert.Equal(ProposalKind.Plan, Assert.Single(_user.Proposals).Kind);
        Assert.Equal(["sudo apt-get install -y nginx"], _terminal.Typed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(3)]
    public async Task Asks_in_Auto_before_a_step_outside_the_plan(int? step)
    {
        using var agent = await RunAsync(ApprovalMode.Auto, Plan(), Run("sudo rm -rf /var/www/html", step));

        var proposal = _user.Proposals[^1];
        Assert.Equal((ProposalKind.RunCommand, true, (int?)null), (proposal.Kind, proposal.IsOutsidePlan, proposal.PlanStep));
    }

    [Fact]
    public async Task Tells_the_user_which_step_a_command_carries_out()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, Plan(), Run("sudo systemctl enable --now nginx", step: 2));

        Assert.Equal((2, false), (_user.Proposals[^1].PlanStep, _user.Proposals[^1].IsOutsidePlan));
    }

    [Fact]
    public async Task Reports_a_declined_plan_with_the_users_words()
    {
        _user.Decide = proposal => CommandApproval.Reject(proposal.Command, "Use the distribution's package, not a PPA.");

        using var agent = await RunAsync(ApprovalMode.Auto, Plan(), Run("sudo apt-get install -y nginx", step: 1));

        Assert.Equal("Not approved. The user said: Use the distribution's package, not a PPA.", _results[0]);
        Assert.StartsWith("Not run: in Auto mode", _results[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_an_empty_plan()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, _ => Call("propose_plan", Arguments(("summary", "Nothing"), ("steps", Array.Empty<string>()), ("rollback", ""))));

        Assert.Equal("Not proposed: give a summary and at least one step.", _results[0]);
        Assert.Empty(_user.Proposals);
    }
}
