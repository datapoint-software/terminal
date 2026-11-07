using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentNewOutputTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sends_output_since_the_last_task_with_the_next_message()
    {
        var terminal = new FakeTerminal();
        var client = new ScriptedChatClient(_ => Text("OK."), _ => Text("OK."));
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("web1", terminal, ApprovalMode.Ask));
        await agent.SendAsync("Hello", Token);
        Assert.Empty(agent.PendingOutput());

        terminal.Advance(3);
        terminal.NewOutput = "make: *** [all] Error 2\nDB_PASSWORD=hunter2hunter2";
        Assert.Equal([new PendingOutput("web1", 3, IsCut: false)], agent.PendingOutput());
        await agent.SendAsync("Why did that fail?", Token);

        var message = client.Requests[1][^1].Text;
        Assert.Contains("[New output in \"web1\" since you last saw it:]\n<terminal_output>\nmake: *** [all] Error 2", message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2hunter2", message, StringComparison.Ordinal);
        Assert.Empty(agent.PendingOutput());
    }
}
