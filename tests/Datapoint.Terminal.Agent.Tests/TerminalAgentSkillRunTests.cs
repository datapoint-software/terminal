using System.Text.Json;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Skills;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentSkillRunTests : IDisposable
{
    private const string Check = "systemctl is-active nginx";

    private readonly string _folder = Directory.CreateTempSubdirectory("skill-run-").FullName;
    private readonly FakeUser _user = new();
    private readonly FakeTerminalOpener _opener = new(
    [
        new AgentConnection("Local", null) { IsLocalShell = true },
        new AgentConnection("web1", "production") { Labels = ["web"] },
        new AgentConnection("web2", "production") { Labels = ["web"] },
        new AgentConnection("web3", "production") { Labels = ["web"] },
        new AgentConnection("db1", "production") { Labels = ["db"] },
    ]);

    private readonly Dictionary<string, FakeTerminal> _terminals = new(StringComparer.Ordinal);
    private readonly List<AgentEventArgs> _events = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Tool(string name, object arguments) =>
        _ => Call(name, JsonDocument.Parse(JsonSerializer.Serialize(arguments)).RootElement.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone(), StringComparer.Ordinal));

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> RunSkill(params string[] hosts) =>
        hosts.Length == 0 ? Tool("run_skill", new { name = "patch" }) : Tool("run_skill", new { name = "patch", hosts });

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Done(string host, bool succeeded = true) =>
        Tool("host_done", new { host, succeeded, summary = succeeded ? "Patched." : "apt failed." });

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Command(string terminal, string command) =>
        Tool("run_command", new { terminal, command, purpose = "Patch." });

    private void WriteSkill(SkillRollout rollout)
    {
        var path = Path.Combine(_folder, "patch", SkillDocument.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, SkillDocument.Create("patch", "Patches a web server.", ["web"], rollout, "# Patch\n\n1. `sudo apt-get upgrade -y`"));
    }

    private static SkillRollout Quick(RolloutFailure onFailure = RolloutFailure.Stop) => new()
    {
        HealthCheck = Check,
        HealthyFor = TimeSpan.Zero,
        Timeout = TimeSpan.FromSeconds(1),
        OnFailure = onFailure,
    };

    private TerminalAgent NewAgent(ScriptedChatClient client)
    {
        var agent = new TerminalAgent(client, _user, new SecretRedactor(), new AgentOptions { HealthCheckInterval = TimeSpan.FromMilliseconds(20) })
        {
            Skills = new SkillFolder(_folder, _ => false, true, () => true),
            TerminalOpener = _opener,
        };
        _opener.Agent = agent;
        _opener.NewTerminal = name => _terminals[name] = new FakeTerminal();
        agent.EventRaised += (_, e) =>
        {
            lock (_events)
            {
                _events.Add(e);
            }
        };
        agent.Mode = ApprovalMode.AskForRisky;
        foreach (var host in new[] { "web1", "web2" })
        {
            _terminals[host] = new FakeTerminal();
            agent.AddTerminal(new AgentTerminal(host, _terminals[host], "production") { Connection = host });
        }

        return agent;
    }

    private static ScriptedChatClient Script(List<string> results, params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] calls)
    {
        var steps = calls.Select((call, index) => index == 0 ? call : messages => { results.Add(LastToolResult(messages)); return call(messages); }).ToList();
        steps.Add(messages => { results.Add(LastToolResult(messages)); return Text("OK."); });
        return new ScriptedChatClient([.. steps]);
    }

    [Fact]
    public async Task Rolls_out_one_host_at_a_time_checking_each_one()
    {
        WriteSkill(Quick());
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill("web1", "web2"), Command("web2", "sudo apt-get upgrade -y"), Done("web1"), Done("web2")));

        await agent.SendAsync("Patch the web servers", Token);

        var proposal = Assert.Single(_user.Proposals);
        Assert.Equal((ProposalKind.Rollout, "web1, web2", "production"), (proposal.Kind, proposal.Terminal, proposal.Environment));
        Assert.StartsWith("1. web1\n2. web2\n", proposal.Command, StringComparison.Ordinal);
        Assert.StartsWith("Approved. Start with web1 (batch 1 of 2).", results[0], StringComparison.Ordinal);
        Assert.Contains("web2 is in the rollout of \"patch\" and its turn hasn't come", results[1], StringComparison.Ordinal);
        Assert.DoesNotContain(_terminals["web2"].Typed, command => command.StartsWith("sudo", StringComparison.Ordinal));
        Assert.Contains("Next: web2 (batch 2 of 2)", results[2], StringComparison.Ordinal);
        Assert.Equal([Check], _terminals["web1"].Typed);
        Assert.StartsWith("web2: healthy for 0s. The rollout is complete.", results[3], StringComparison.Ordinal);
        Assert.Null(agent.Rollout);
        Assert.Contains(_events, e => e.Kind == AgentEventKind.RolloutChanged && e.Rollout!.Outcome == RolloutOutcome.Completed);
    }

    [Fact]
    public async Task Runs_on_every_host_with_the_labels_when_none_are_named()
    {
        WriteSkill(Quick() with { Order = RolloutOrder.Batches, BatchSize = 2 });
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill()));

        await agent.SendAsync("Patch the web servers", Token);

        Assert.StartsWith("1. web1, web2\n2. web3\n", Assert.Single(_user.Proposals).Command, StringComparison.Ordinal);
        Assert.StartsWith("Approved. Start with web1, web2 (batch 1 of 2).", results[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("db1", "\"db1\" isn't one of the hosts the skill runs on: web1, web2, web3.")]
    [InlineData("Local", "\"Local\" isn't one of the hosts the skill runs on: web1, web2, web3.")]
    public async Task Refuses_hosts_without_the_skills_labels(string host, string problem)
    {
        WriteSkill(Quick());
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill(host)));

        await agent.SendAsync("Patch", Token);

        Assert.Equal("Not started: " + problem, results[0]);
        Assert.Empty(_user.Proposals);
    }

    [Fact]
    public async Task Refuses_more_hosts_at_once_than_the_skill_allows()
    {
        WriteSkill(Quick() with { Order = RolloutOrder.AllAtOnce, MaxHosts = 2 });
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill()));

        await agent.SendAsync("Patch", Token);

        Assert.StartsWith("Not started: the skill runs on at most 2 hosts at once.", results[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_after_a_failing_health_check_and_skips_the_rest()
    {
        WriteSkill(Quick());
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill("web1", "web2"), Done("web1")));
        _terminals["web1"].Run = _ => ("failed\nnginx.service failed", 3);

        await agent.SendAsync("Patch", Token);

        Assert.StartsWith("The rollout stopped after a failure.", results[1], StringComparison.Ordinal);
        Assert.Contains("web1: failed (the health check kept failing for 1s: exit code 3, output \"failed / nginx.service failed\").", results[1], StringComparison.Ordinal);
        Assert.Contains("web2: skipped, not started.", results[1], StringComparison.Ordinal);
        Assert.Empty(_terminals["web2"].Typed);
    }

    [Fact]
    public async Task Asks_the_user_when_the_skill_says_so()
    {
        WriteSkill(Quick(RolloutFailure.Ask));
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill("web1", "web2"), Done("web1", succeeded: false)));
        _user.Answer = question => question.Options[^1].Label;

        await agent.SendAsync("Patch", Token);

        var question = Assert.Single(_user.Questions);
        Assert.Equal("web1 failed. What should the rollout do?", question.Text);
        Assert.Equal("Stop the rollout|Skip web1 and go on with web2", string.Join("|", question.Options.Select(option => option.Label)));
        Assert.Equal("apt failed.", question.Details);
        Assert.Contains("web1: failed (apt failed.). Next: web2", results[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Passes_an_answer_in_the_users_own_words_to_the_agent()
    {
        WriteSkill(Quick(RolloutFailure.Ask));
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill("web1", "web2"), Done("web1", succeeded: false)));
        _user.Answer = _ => "Roll web1 back first";

        await agent.SendAsync("Patch", Token);

        Assert.StartsWith("The rollout stopped after a failure.", results[1], StringComparison.Ordinal);
        Assert.EndsWith("Asked what to do, the user said: Roll web1 back first", results[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Offers_to_check_again_after_a_failing_health_check()
    {
        WriteSkill(Quick(RolloutFailure.Ask));
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill("web1", "web2"), Done("web1")));
        _terminals["web1"].Run = _ => _user.Questions.Count == 0 ? ("degraded", 1) : ("running", 0);
        _user.Answer = question => question.Options[1].Label;

        await agent.SendAsync("Patch", Token);

        Assert.Equal("Check again for 1s", _user.Questions[0].Options[1].Label);
        Assert.Contains("web1: healthy for 0s. Next: web2", results[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reconnects_a_host_that_went_away_for_a_reboot()
    {
        WriteSkill(Quick() with { Timeout = TimeSpan.FromSeconds(5) });
        var results = new List<string>();
        TerminalAgent? started = null;
        using var agent = NewAgent(Script(
            results,
            RunSkill("web1", "web2"),
            messages =>
            {
                started!.RemoveTerminal(started.Terminals.Single(terminal => string.Equals(terminal.Name, "web1", StringComparison.Ordinal)));
                return Done("web1")(messages);
            }));
        started = agent;
        _opener.FailedReconnects = 2;

        await agent.SendAsync("Patch", Token);

        Assert.Equal(3, _opener.Reconnects.Count(host => string.Equals(host, "web1", StringComparison.Ordinal)));
        Assert.Contains(_events, e => e.Kind == AgentEventKind.Notice && e.Text.StartsWith("Reconnected to web1 after", StringComparison.Ordinal));
        Assert.Contains("web1: healthy for 0s. Next: web2", results[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_when_the_user_stops_it_during_a_check()
    {
        WriteSkill(Quick() with { Timeout = TimeSpan.FromMinutes(5) });
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill("web1", "web2"), Done("web1")));
        _terminals["web1"].Run = _ =>
        {
            agent.StopRollout();
            return ("degraded", 1);
        };

        await agent.SendAsync("Patch", Token);

        Assert.StartsWith("The user stopped the rollout.", results[1], StringComparison.Ordinal);
        Assert.Contains("web2: skipped, not started.", results[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Waits_after_the_batch_while_paused()
    {
        WriteSkill(Quick());
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill("web1", "web2"), Done("web1")));
        agent.EventRaised += (_, e) =>
        {
            if (e is { Kind: AgentEventKind.RolloutChanged, Rollout.Batch: 1 } && e.Rollout.Hosts[0].State == RolloutHostState.Working && !e.Rollout.IsPaused)
            {
                agent.PauseRollout(true);
            }

            if (e is not { Kind: AgentEventKind.StatusChanged, Status: AgentStatus.WaitingForUser })
            {
                return;
            }

            agent.PauseRollout(false);
        };

        await agent.SendAsync("Patch", Token);

        Assert.Contains(_events, e => e is { Kind: AgentEventKind.RolloutChanged, Rollout.IsPaused: true });
        Assert.Contains("Next: web2", results[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_a_rollout_going_into_the_next_task()
    {
        WriteSkill(Quick());
        var results = new List<string>();
        var client = Script(results, RunSkill("web1", "web2"));
        using var agent = NewAgent(client);

        await agent.SendAsync("Patch", Token);
        await agent.SendAsync("Go on", Token);

        Assert.Equal(RolloutOutcome.Running, agent.Rollout!.Outcome);
        Assert.Contains("[Rollout of the skill \"patch\" in progress, batch 1 of 2: web1 working; web2 waiting.", client.Requests[^1][^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_a_second_rollout_while_one_is_going()
    {
        WriteSkill(Quick());
        var results = new List<string>();
        using var agent = NewAgent(Script(results, RunSkill("web1"), RunSkill("web2")));

        await agent.SendAsync("Patch", Token);

        Assert.StartsWith("Not started: the rollout of \"patch\" is still going.", results[1], StringComparison.Ordinal);
    }
}
