using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

// Product decision 136.
public sealed class TerminalAgentReadsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> ReadScreen(string terminal, string id) =>
        _ => Call("read_screen", new Dictionary<string, object?>(StringComparer.Ordinal) { ["terminal"] = terminal }, id);

    [Fact]
    public async Task Says_a_screen_is_unchanged_instead_of_sending_it_again()
    {
        var web1 = new FakeTerminal { Screen = "$ systemctl status nginx\nactivating (start)" };
        var results = new List<string>();
        var client = new ScriptedChatClient(
            ReadScreen("web1", "a"),
            messages => { results.Add(LastToolResult(messages)); return ReadScreen("web1", "b")(messages); },
            messages => { results.Add(LastToolResult(messages)); web1.Screen = "$ systemctl status nginx\nactive (running)"; return ReadScreen("web1", "c")(messages); },
            messages => { results.Add(LastToolResult(messages)); return Text("nginx is up."); });
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("web1", web1));

        await agent.SendAsync("Wait for nginx on web1", Token);

        Assert.Contains("activating (start)", results[0], StringComparison.Ordinal);
        Assert.EndsWith("The screen of \"web1\" is unchanged since your last read_screen of it.", results[1], StringComparison.Ordinal);
        Assert.Contains("active (running)", results[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sends_the_screen_again_in_a_new_conversation()
    {
        var results = new List<string>();
        var client = new ScriptedChatClient(
            ReadScreen("web1", "a"),
            messages => { results.Add(LastToolResult(messages)); return Text("OK."); },
            ReadScreen("web1", "b"),
            messages => { results.Add(LastToolResult(messages)); return Text("OK."); });
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("web1", new FakeTerminal()));

        await agent.SendAsync("Look at web1", Token);
        agent.ClearConversation();
        await agent.SendAsync("Look again", Token);

        Assert.All(results, result => Assert.Contains("<terminal_output>", result, StringComparison.Ordinal));
    }
}
