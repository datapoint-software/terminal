using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

// Product decision 139.
public sealed class TerminalAgentSweepTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Journal(int step) =>
        _ => Call("run_command", new Dictionary<string, object?>(StringComparer.Ordinal) { ["terminal"] = "web1", ["command"] = $"journalctl -n {step}", ["purpose"] = "to check" }, $"call-{step}");

    private static async Task<IReadOnlyList<ChatMessage>> RunAsync(int commands, bool keepsHistory)
    {
        IReadOnlyList<ChatMessage>? last = null;
        var steps = Enumerable.Range(1, commands).Select(Journal).ToList();
        steps.Add(messages => { last = [.. messages]; return Text("Done."); });
        var terminal = new FakeTerminal { Run = command => (command + "\n" + new string('x', 3_000), 0) };
        using var agent = new TerminalAgent(new ScriptedChatClient([.. steps]), new FakeUser(), new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("web1", terminal));
        var next = 100L;
        agent.EventRaised += (_, e) => e.HistoryId = keepsHistory && e.Kind == AgentEventKind.CommandFinished ? next++ : e.HistoryId;

        await agent.SendAsync("Look through web1's journal", Token);
        return last!;
    }

    private static List<string> Results(IReadOnlyList<ChatMessage> messages) =>
        [.. messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Select(result => (string)result.Result!)];

    [Fact]
    public async Task Points_to_the_history_for_long_outputs_many_replies_old()
    {
        var results = Results(await RunAsync(16, keepsHistory: true));

        Assert.Equal(16, results.Count);
        Assert.Equal(
            "Exit code: 0\n[Output left out to keep the conversation short. It's kept as history entry #100: read_history with entry 100 reads it, by line range or the lines containing some text.]",
            results[0]);
        // Outputs 1 to 7 reached 21,000 characters eight replies on; output 8 alone isn't worth another cache write yet.
        Assert.All(results.Take(7), result => Assert.DoesNotContain("<terminal_output>", result, StringComparison.Ordinal));
        Assert.All(results.Skip(7), result => Assert.Contains("<terminal_output>", result, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Keeps_outputs_until_enough_has_built_up_or_without_a_history()
    {
        Assert.All(Results(await RunAsync(12, keepsHistory: true)), result => Assert.Contains("<terminal_output>", result, StringComparison.Ordinal));
        Assert.All(Results(await RunAsync(16, keepsHistory: false)), result => Assert.Contains("<terminal_output>", result, StringComparison.Ordinal));
    }
}
