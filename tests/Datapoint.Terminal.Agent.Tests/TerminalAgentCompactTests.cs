using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

// Product decision 133.
public sealed class TerminalAgentCompactTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ChatResponse WithUsage(ChatResponse response, long input)
    {
        response.Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = 100 };
        return response;
    }

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> ReadScreen(long input) =>
        _ => WithUsage(Call("read_screen", new Dictionary<string, object?>(StringComparer.Ordinal) { ["terminal"] = "web1" }), input);

    private static TerminalAgent NewAgent(ScriptedChatClient client, List<AgentEventArgs> events)
    {
        var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor()) { CompactAtTokens = 50_000 };
        agent.AddTerminal(new AgentTerminal("web1", new FakeTerminal()));
        agent.EventRaised += (_, e) => events.Add(e);
        return agent;
    }

    [Fact]
    public async Task Carries_on_from_a_summary_once_the_conversation_passes_the_limit()
    {
        IReadOnlyList<ChatMessage>? summarized = null;
        IReadOnlyList<ChatMessage>? after = null;
        var client = new ScriptedChatClient(
            ReadScreen(60_000),
            messages => { summarized = [.. messages]; return WithUsage(Text("Checked web1's disk: / is 91% full."), 60_000); },
            messages => { after = [.. messages]; return Text("Done."); });
        var events = new List<AgentEventArgs>();
        using var agent = NewAgent(client, events);

        await agent.SendAsync("Free some space on web1", Token);

        Assert.StartsWith("[The conversation has grown long", summarized![^1].Text, StringComparison.Ordinal);
        Assert.Equal(2, after!.Count);
        var carried = after[1].Text;
        Assert.Contains("<user_message>\nFree some space on web1\n</user_message>", carried, StringComparison.Ordinal);
        Assert.Contains("<conversation_summary>\nChecked web1's disk: / is 91% full.\n</conversation_summary>", carried, StringComparison.Ordinal);
        var notice = Assert.Single(events, e => e.Kind == AgentEventKind.Notice);
        Assert.Equal("Checked web1's disk: / is 91% full.", notice.Output);
        Assert.Equal(3, agent.Usage.Session.Requests);
    }

    // The context size isn't known from the summary request until the first reply after it, so summarizing lasts until then.
    [Fact]
    public async Task Is_summarizing_from_the_request_until_the_next_reply_reports_its_size()
    {
        var seen = new List<bool>();
        TerminalAgent? agent = null;
        var client = new ScriptedChatClient(
            ReadScreen(60_000),
            _ => { seen.Add(agent!.IsSummarizing); return WithUsage(Text("Checked web1."), 60_000); },
            _ => { seen.Add(agent!.IsSummarizing); return WithUsage(Text("Done."), 2_000); });
        var events = new List<AgentEventArgs>();
        using var created = NewAgent(client, events);
        agent = created;

        await agent.SendAsync("Check web1", Token);

        Assert.Equal([true, true], seen);
        Assert.False(agent.IsSummarizing);
        Assert.Equal(2_100, agent.Usage.ContextTokens);
    }

    [Fact]
    public async Task Keeps_the_conversation_below_the_limit_or_when_set_to_never()
    {
        var client = new ScriptedChatClient(ReadScreen(60_000), _ => Text("Done."));
        var events = new List<AgentEventArgs>();
        using var agent = NewAgent(client, events);
        agent.CompactAtTokens = null;

        await agent.SendAsync("Check web1", Token);

        Assert.Equal(2, client.Requests.Count);
        Assert.True(client.Requests[1].Count > 2);
        Assert.DoesNotContain(events, e => e.Kind == AgentEventKind.Notice);
    }

    [Fact]
    public async Task Asks_for_a_summary_once_a_task_when_none_comes_back()
    {
        var client = new ScriptedChatClient(ReadScreen(60_000), ReadScreen(60_000), ReadScreen(60_000), _ => Text("Done."));
        var events = new List<AgentEventArgs>();
        using var agent = NewAgent(client, events);

        await agent.SendAsync("Check web1", Token);

        Assert.Equal(4, client.Requests.Count);
        Assert.Contains("[The conversation has grown long", client.Requests[1][^1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain(client.Requests.Skip(2), request => request[^1].Text.Contains("[The conversation has grown long", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, null, 100_000)]
    [InlineData(null, 64_000, 48_000)]
    [InlineData(200_000, null, 200_000)]
    [InlineData(1_000, null, 20_000)]
    [InlineData(0, 200_000, null)]
    public void Summarizes_at_the_chosen_size_but_before_a_small_window_fills(int? chosen, int? window, int? expected) =>
        Assert.Equal(expected, CompactionLimits.For(window, chosen));
}
