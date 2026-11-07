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
        agent.Mode = mode;
        agent.AddTerminal(new AgentTerminal("web1", _terminal, "production"));
        agent.AddTerminal(new AgentTerminal("web2", new FakeTerminal(), "production"));
        agent.EventRaised += (_, e) => _events.Add(e);
        await agent.SendAsync("Install nginx", Token);
        return agent;
    }

    [Fact]
    public async Task Shows_the_plan_for_approval_and_reports_it_once_approved()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, Plan());

        var proposal = Assert.Single(_user.Proposals);
        Assert.Equal((ProposalKind.Plan, "Install nginx", "web1", "production"), (proposal.Kind, proposal.Purpose, proposal.Terminal, proposal.Environment));
        Assert.Equal(
            "1. sudo apt-get install -y nginx: install it\n2. sudo systemctl enable --now nginx: start it\n\nTo roll back: sudo apt-get remove nginx",
            proposal.Command);
        Assert.StartsWith("Approved.", _results[0], StringComparison.Ordinal);
        var approved = Assert.Single(_events, e => e.Kind == AgentEventKind.PlanApproved);
        Assert.Equal(("Install nginx", "web1", 2), (approved.Text, approved.Terminal, approved.Plan!.Steps.Count));
        Assert.Null(agent.Plan);
    }

    [Fact]
    public async Task Reads_each_steps_command_purpose_and_warning()
    {
        using var steps = System.Text.Json.JsonDocument.Parse("""
            [{"command":"sudo reboot","purpose":"Start the new kernel","warning":"Ends the SSH session"},
             {"command":" uname -r ","purpose":""},
             "uptime"]
            """);
        using var agent = await RunAsync(ApprovalMode.Ask, _ => Call("propose_plan", Arguments(("summary", "Update"), ("steps", steps.RootElement.Clone()), ("rollback", "None."))));

        Assert.Equal(
            [new AgentPlanStep("sudo reboot", "Start the new kernel", "Ends the SSH session"), new AgentPlanStep("uname -r", string.Empty), new AgentPlanStep("uptime", string.Empty)],
            _user.Proposals.Single().Plan!.Steps);
    }

    [Fact]
    public async Task Refuses_a_step_without_a_command()
    {
        using var steps = System.Text.Json.JsonDocument.Parse("""[{"purpose":"Think about it"}]""");
        using var agent = await RunAsync(ApprovalMode.Ask, _ => Call("propose_plan", Arguments(("summary", "Update"), ("steps", steps.RootElement.Clone()), ("rollback", ""))));

        Assert.Equal("Not proposed: give every step the command you'll run, or an example of it.", _results[0]);
        Assert.Empty(_user.Proposals);
    }

    [Fact]
    public async Task Reads_each_steps_title_hosts_and_whether_its_command_is_an_example()
    {
        using var steps = System.Text.Json.JsonDocument.Parse("""
            [{"title":"Update the kernel","terminals":["web1"],"command":"sudo apt-get install linux-image-generic","example":true,"purpose":"Newer kernel"},
             {"title":"Check","terminals":"web1","command":"uname -r","purpose":"Confirm"}]
            """);
        using var agent = await RunAsync(ApprovalMode.Ask, _ => Call("propose_plan", Arguments(("summary", "Update"), ("findings", "- web1 runs 6.8.0-45"), ("steps", steps.RootElement.Clone()), ("rollback", "None."))));

        var plan = _user.Proposals.Single().Plan!;
        Assert.Equal("- web1 runs 6.8.0-45", plan.Findings);
        Assert.Equal(("Update the kernel", true, "web1"), (plan.Steps[0].Title, plan.Steps[0].IsExample, plan.Steps[0].Terminals.Single()));
        Assert.Equal(("Check", false, "web1"), (plan.Steps[1].Title, plan.Steps[1].IsExample, plan.Steps[1].Terminals.Single()));
        Assert.StartsWith("1. On web1: sudo apt-get install", plan.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_a_step_on_a_host_it_doesnt_know()
    {
        using var steps = System.Text.Json.JsonDocument.Parse("""[{"title":"Restart","terminals":["db9"],"command":"sudo reboot","purpose":"Update"}]""");
        using var agent = await RunAsync(ApprovalMode.Ask, _ => Call("propose_plan", Arguments(("summary", "Update"), ("steps", steps.RootElement.Clone()), ("rollback", ""))));

        Assert.Equal("Not proposed: \"db9\" is neither an open terminal nor a listed connection.", _results[0]);
        Assert.Empty(_user.Proposals);
    }

    [Fact]
    public async Task Asks_when_a_planned_step_runs_on_a_host_it_doesnt_name()
    {
        using var steps = System.Text.Json.JsonDocument.Parse("""[{"title":"Install","terminals":["web2"],"command":"sudo apt-get install -y nginx","purpose":"Install it"}]""");
        var plan = (Func<IReadOnlyList<ChatMessage>, ChatResponse>)(_ => Call("propose_plan", Arguments(("summary", "Install"), ("steps", steps.RootElement.Clone()), ("rollback", ""))));

        using var agent = await RunAsync(ApprovalMode.Plan, plan, Run("sudo apt-get install -y nginx", step: 1));

        Assert.Equal([ProposalKind.Plan, ProposalKind.RunCommand], _user.Proposals.Select(proposal => proposal.Kind));
        Assert.True(_user.Proposals[1].IsOutsidePlan);
    }

    [Theory]
    [InlineData(0, PlanStepState.Succeeded)]
    [InlineData(2, PlanStepState.Failed)]
    public async Task Reports_which_step_runs_and_how_it_went(int exitCode, PlanStepState expected)
    {
        _terminal.Run = command => ("output", exitCode);

        using var agent = await RunAsync(ApprovalMode.Ask, Plan(), Run("sudo systemctl enable --now nginx", step: 2));

        var started = Assert.Single(_events, e => e.Kind == AgentEventKind.CommandStarted);
        var finished = Assert.Single(_events, e => e.Kind == AgentEventKind.CommandFinished);
        Assert.Equal((2, PlanStepState.Running), (started.PlanStep, started.StepState));
        Assert.Equal((2, expected), (finished.PlanStep, finished.StepState));
    }

    [Fact]
    public async Task Refuses_changes_in_Plan_mode_without_a_plan()
    {
        using var agent = await RunAsync(ApprovalMode.Plan, Run("sudo apt-get install -y nginx"), Run("uptime"));

        Assert.Equal("Not run: in Plan mode, show your plan with propose_plan before changing anything.", _results[0]);
        Assert.Equal(["uptime"], _terminal.Typed);
        Assert.Empty(_user.Proposals);
    }

    [Fact]
    public async Task Runs_planned_steps_in_Plan_mode_without_asking_again()
    {
        using var agent = await RunAsync(ApprovalMode.Plan, Plan(), Run("sudo apt-get install -y nginx", step: 1));

        Assert.Equal(ProposalKind.Plan, Assert.Single(_user.Proposals).Kind);
        Assert.Equal(["sudo apt-get install -y nginx"], _terminal.Typed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(3)]
    public async Task Asks_in_Plan_mode_before_a_step_outside_the_plan(int? step)
    {
        using var agent = await RunAsync(ApprovalMode.Plan, Plan(), Run("sudo rm -rf /var/www/html", step));

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
    public async Task A_revised_plan_carries_the_one_before_it_and_its_hosts_environments()
    {
        using var steps = System.Text.Json.JsonDocument.Parse("""[{"title":"Check","terminals":["web1","web2"],"command":"uname -r","purpose":"Confirm"}]""");
        _user.Decide = proposal => _user.Proposals.Count == 1 ? CommandApproval.Reject(proposal.Command, "Pin the version.") : CommandApproval.Approve(proposal.Command);
        ChatResponse Propose(IReadOnlyList<ChatMessage> messages) => Call("propose_plan", Arguments(("summary", "Update"), ("steps", steps.RootElement.Clone()), ("rollback", "None.")));

        using var agent = await RunAsync(ApprovalMode.Ask, Propose, Propose);

        Assert.Null(_user.Proposals[0].PreviousPlan);
        Assert.Same(_user.Proposals[0].Plan, _user.Proposals[1].PreviousPlan);
        Assert.Equal(("production", "production"), (_user.Proposals[1].Environments["web1"], _user.Proposals[1].Environments["WEB2"]));
    }

    [Fact]
    public async Task Reports_a_declined_plan_with_the_users_words()
    {
        _user.Decide = proposal => CommandApproval.Reject(proposal.Command, "Use the distribution's package, not a PPA.");

        using var agent = await RunAsync(ApprovalMode.Plan, Plan(), Run("sudo apt-get install -y nginx", step: 1));

        Assert.Equal("Not approved. The user said: Use the distribution's package, not a PPA.", _results[0]);
        Assert.StartsWith("Not run: in Plan mode", _results[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_an_empty_plan()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, _ => Call("propose_plan", Arguments(("summary", "Nothing"), ("steps", Array.Empty<string>()), ("rollback", ""))));

        Assert.Equal("Not proposed: give a summary and at least one step.", _results[0]);
        Assert.Empty(_user.Proposals);
    }
}
