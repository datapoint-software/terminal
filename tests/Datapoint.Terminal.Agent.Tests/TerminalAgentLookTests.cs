using System.Text.Json;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentLookTests
{
    private readonly FakeUser _user = new();
    private readonly FakeTerminal _web1 = new();
    private readonly FakeTerminal _web2 = new();
    private readonly FakeTerminalOpener _opener = new([new AgentConnection("db1", "production")]);
    private readonly List<string?> _results = [];
    private readonly List<AgentEventArgs> _events = [];

    private const string Message = "Before I plan the drain I'd like to look at web1 and db1, to see what would have to move.";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, object?> Arguments(params (string Name, object? Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Look(params string[] hosts) => _ => Call("ask_to_look", LookArguments(hosts));

    private static Dictionary<string, object?> LookArguments(params string[] hosts)
    {
        var json = JsonSerializer.Serialize(hosts.Select(host => new { host, reason = "To see what runs there, since the plan moves it." }));
        return Arguments(("message", Message), ("title", "Drain web1"), ("hosts", JsonDocument.Parse(json).RootElement.Clone()));
    }

    private IEnumerable<string> Replies => _events.Where(e => e.Kind == AgentEventKind.Reply).Select(e => e.Text);

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Run(string terminal, string command) =>
        _ => Call("run_command", Arguments(("terminal", terminal), ("command", command), ("purpose", "look")));

    private Func<IReadOnlyList<ChatMessage>, ChatResponse> Record(Func<IReadOnlyList<ChatMessage>, ChatResponse> next) =>
        messages =>
        {
            _results.Add(LastToolResult(messages));
            return next(messages);
        };

    private async Task<TerminalAgent> RunAsync(ApprovalMode mode, params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] steps)
    {
        var script = steps.Select((step, index) => index == 0 ? step : Record(step)).Append(Record(_ => Text("Done."))).ToArray();
        var agent = new TerminalAgent(new ScriptedChatClient(script), _user, new SecretRedactor()) { TerminalOpener = _opener, Mode = mode };
        _opener.Agent = agent;
        agent.AddTerminal(new AgentTerminal("web1", _web1, "production"));
        agent.AddTerminal(new AgentTerminal("web2", _web2, "production"));
        agent.EventRaised += (_, e) => _events.Add(e);
        await agent.SendAsync("Drain web1", Token);
        return agent;
    }

    [Fact]
    public async Task Asks_in_plain_words_which_hosts_it_will_look_at_and_why()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, Look("web1", "db1"));

        var proposal = Assert.Single(_user.Proposals);
        Assert.Equal(ProposalKind.Look, proposal.Kind);
        Assert.Equal("Drain web1", proposal.Look!.Title);
        Assert.Equal([("web1", "production", false), ("db1", "production", true)], proposal.Look.Hosts.Select(host => (host.Name, host.Environment, host.IsConnection)));
        Assert.Equal("Allowed. Read-only commands on web1, db1 run without asking until the task ends. Open db1 with open_terminal first. Run one simple command at a time, then propose your plan with what you found.", _results[0]);
    }

    [Fact]
    public async Task Says_why_it_wants_to_look_before_asking()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, Look("web1", "db1"));

        var said = _events.FindIndex(e => e.Kind == AgentEventKind.Reply && string.Equals(e.Text, Message, StringComparison.Ordinal));
        var asked = _events.FindIndex(e => e.Kind == AgentEventKind.StatusChanged && e.Status == AgentStatus.WaitingForApproval);
        Assert.InRange(said, 0, asked - 1);
    }

    [Fact]
    public async Task Doesnt_repeat_itself_when_it_already_wrote_why()
    {
        var both = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new TextContent("Let me look first."), new FunctionCallContent("call-1", "ask_to_look", LookArguments("web1"))]));
        using var agent = await RunAsync(ApprovalMode.Ask, _ => both);

        Assert.Equal(["Let me look first.", "Done."], Replies.ToList());
    }

    [Fact]
    public async Task Says_what_it_found_before_its_plan()
    {
        var plan = Arguments(("summary", "Drain web1"), ("findings", "- web1 runs the API."), ("steps", JsonDocument.Parse("""[{"command":"docker node update --availability drain web1","purpose":"Move its tasks","terminals":["web1"]}]""").RootElement.Clone()));
        using var agent = await RunAsync(ApprovalMode.Ask, _ => Call("propose_plan", plan));

        var said = _events.FindIndex(e => e.Kind == AgentEventKind.Reply && string.Equals(e.Text, "- web1 runs the API.", StringComparison.Ordinal));
        var asked = _events.FindIndex(e => e.Kind == AgentEventKind.StatusChanged && e.Status == AgentStatus.WaitingForApproval);
        Assert.InRange(said, 0, asked - 1);
    }

    [Fact]
    public async Task Runs_read_only_commands_on_those_hosts_without_asking_once_allowed()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, Look("web1"), Run("web1", "uptime"), Run("web1", "sudo systemctl restart nginx"), Run("web2", "uptime"));

        Assert.Equal(["uptime", "sudo systemctl restart nginx"], _web1.Typed);
        Assert.Equal(["uptime"], _web2.Typed);
        Assert.Equal([ProposalKind.Look, ProposalKind.RunCommand, ProposalKind.RunCommand], _user.Proposals.Select(proposal => proposal.Kind));
        Assert.Equal(["sudo systemctl restart nginx", "uptime"], _user.Proposals.Skip(1).Select(proposal => proposal.Command), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Opens_a_connection_it_may_look_at_without_asking()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, Look("db1"), _ => Call("open_terminal", Arguments(("connection", "db1"), ("purpose", "look"))), Run("db1", "uptime"));

        Assert.Equal(["db1"], _opener.Opened);
        Assert.Equal(ProposalKind.Look, Assert.Single(_user.Proposals).Kind);
    }

    [Fact]
    public async Task Reports_what_the_user_said_when_they_dont_allow_it()
    {
        _user.Decide = proposal => CommandApproval.Reject(proposal.Command, "Only web2.");

        using var agent = await RunAsync(ApprovalMode.Ask, Look("web1"), Run("web1", "uptime"));

        Assert.Equal("Not allowed. The user said: Only web2.", _results[0]);
    }

    [Fact]
    public async Task Ends_with_the_task()
    {
        var client = new ScriptedChatClient(Look("web1"), _ => Text("Looked."), Run("web1", "uptime"), _ => Text("Done."));
        using var agent = new TerminalAgent(client, _user, new SecretRedactor()) { TerminalOpener = _opener };
        agent.AddTerminal(new AgentTerminal("web1", _web1, "production"));
        await agent.SendAsync("Drain web1", Token);

        await agent.SendAsync("Check again", Token);

        Assert.Equal([ProposalKind.Look, ProposalKind.RunCommand], _user.Proposals.Select(proposal => proposal.Kind));
    }

    [Fact]
    public async Task Refuses_a_host_that_is_neither_open_nor_a_connection()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, Look("web9"));

        Assert.Equal("Not asked: \"web9\" is neither an open terminal nor a listed connection.", _results[0]);
        Assert.Empty(_user.Proposals);
    }

    [Fact]
    public async Task Doesnt_ask_when_the_user_ignores_permissions()
    {
        using var agent = await RunAsync(ApprovalMode.IgnorePermissions, Look("web1"));

        Assert.Empty(_user.Proposals);
        Assert.StartsWith("Allowed.", _results[0], StringComparison.Ordinal);
    }
}
