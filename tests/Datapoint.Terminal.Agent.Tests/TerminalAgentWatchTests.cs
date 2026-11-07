using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentWatchTests
{
    private static readonly string[] s_rollout = ["rolled out", "ERROR"];
    private static readonly string[] s_done = ["done"];
    private static readonly string[] s_blank = [" "];

    private readonly FakeTerminal _terminal = new() { IsAtPrompt = false };
    private readonly List<AgentEventArgs> _events = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class SteppingClock(TimeSpan step) : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now += step;
    }

    private async Task<string?> WatchAsync(object? until, int? minutes = null, TimeProvider? clock = null, Action? meanwhile = null)
    {
        string? result = null;
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal) { ["terminal"] = "deploy", ["until"] = until };
        if (minutes is not null)
        {
            arguments["minutes"] = minutes;
        }

        var client = new ScriptedChatClient(_ => Call("watch_terminal", arguments), messages => { result = LastToolResult(messages); return Text("OK."); });
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor()) { Clock = clock ?? new SteppingClock(TimeSpan.Zero) };
        agent.AddTerminal(new AgentTerminal("deploy", _terminal));
        agent.EventRaised += (_, e) => _events.Add(e);
        var sending = agent.SendAsync("Tell me when it's rolled out", Token);
        if (meanwhile is not null)
        {
            await Task.Delay(100, Token);
            meanwhile();
        }

        await sending.WaitAsync(TimeSpan.FromSeconds(10), Token);
        return result;
    }

    [Fact]
    public async Task Reports_the_first_line_that_matches()
    {
        var result = await WatchAsync(s_rollout, meanwhile: () => _terminal.Print("Waiting for rollout\nerror: image not found\nmore\n"));

        Assert.Equal("Seen after 0 s. The last lines:\n<terminal_output>\nWaiting for rollout\nerror: image not found\n</terminal_output>", result);
        Assert.Contains(_events, e => e.Kind == AgentEventKind.Notice && string.Equals(e.Text, "Watching deploy for \"rolled out\" or \"ERROR\", for up to 30 minutes.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stops_when_the_command_finishes()
    {
        _terminal.IsAtPrompt = true;

        var result = await WatchAsync(s_done);

        Assert.StartsWith("The command finished after 0 s without anything matching.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_when_the_terminal_closes()
    {
        var result = await WatchAsync(s_done, meanwhile: _terminal.End);

        Assert.StartsWith("The terminal closed before anything matched.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gives_up_after_the_time_given()
    {
        var result = await WatchAsync(s_done, minutes: 1, clock: new SteppingClock(TimeSpan.FromSeconds(61)));

        Assert.StartsWith("Nothing matched in 1 min; the command is still running.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Needs_something_to_watch_for()
    {
        Assert.Equal("Not watched: give at least one text to watch for.", await WatchAsync(s_blank));
    }
}
