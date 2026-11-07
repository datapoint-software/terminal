using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Datapoint.Terminal.Agent.Usage;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentUsageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ChatResponse WithUsage(ChatResponse response, long input, long output)
    {
        response.Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output };
        return response;
    }

    [Fact]
    public async Task Counts_each_request_against_the_terminal_it_worked_in()
    {
        var client = new ScriptedChatClient(
            _ => WithUsage(Call("read_screen", new Dictionary<string, object?>(StringComparer.Ordinal) { ["terminal"] = "web1" }), 1000, 50),
            _ => WithUsage(Text("All quiet."), 1200, 20));
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("web1", new FakeTerminal()));
        agent.Usage.Price = new TokenPrice(2m, 10m);

        await agent.SendAsync("Anything wrong on web1?", Token);

        Assert.Equal(2270, agent.Usage.Session.Tokens);
        Assert.Equal(2, agent.Usage.Terminals["web1"].Requests);
        Assert.Equal(((2200 * 2m) + (70 * 10m)) / 1_000_000m, agent.Usage.Session.Cost);
    }

    [Fact]
    public async Task Stops_before_the_next_request_once_the_limit_is_reached()
    {
        var client = new ScriptedChatClient(
            _ => WithUsage(Call("read_screen", new Dictionary<string, object?>(StringComparer.Ordinal) { ["terminal"] = "web1" }), 1_000_000, 0),
            _ => Text("Never asked."));
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("web1", new FakeTerminal()));
        agent.Usage.Price = new TokenPrice(3m, 15m);
        agent.Usage.Budget = 1m;
        var notices = new List<string>();
        agent.EventRaised += (_, e) => notices.Add(e.Text);

        await agent.SendAsync("Check web1.", Token);

        Assert.Single(client.Requests);
        Assert.Contains(notices, notice => notice.Contains("spending limit", StringComparison.Ordinal));
    }
}
