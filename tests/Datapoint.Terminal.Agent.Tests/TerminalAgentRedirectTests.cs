using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentRedirectTests
{
    private readonly FakeTerminal _terminal = new();
    private readonly FakeUser _user = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, object?> Arguments(params (string Name, object? Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

    private static List<string> ToolResults(IReadOnlyList<ChatMessage> messages) =>
        [.. messages.Last(message => message.Role == ChatRole.Tool).Contents.OfType<FunctionResultContent>().Select(result => result.Result?.ToString() ?? string.Empty)];

    private TerminalAgent Create(ScriptedChatClient client)
    {
        var agent = new TerminalAgent(client, _user, new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("Local", _terminal));
        return agent;
    }

    // What the chat does when the user sends a message while a card waits: the agent takes it and the card's wait ends.
    private static T SendInstead<T>(TerminalAgent agent, string message)
    {
        Assert.True(agent.IsWaitingForUser);
        Assert.True(agent.Redirect(message));
        throw new OperationCanceledException();
    }

    [Fact]
    public async Task A_message_sent_instead_of_an_answer_becomes_the_new_goal_and_skips_the_rest_of_the_step()
    {
        List<string>? results = null;
        var client = new ScriptedChatClient(
            _ => new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent("call-1", "ask_user", Arguments(("question", "Which environment?"))),
                new FunctionCallContent("call-2", "run_command", Arguments(("command", "deploy"), ("purpose", "deploy"))),
            ])),
            messages => { results = ToolResults(messages); return Text("Looking at the logs."); });
        using var agent = Create(client);
        _user.Answer = _ => SendInstead<string?>(agent, "Check the logs instead");

        await agent.SendAsync("Deploy", Token);

        Assert.StartsWith("Cancelled: instead of answering, the user sent a new message.", results![0], StringComparison.Ordinal);
        Assert.EndsWith("Their message:\nCheck the logs instead", results[0], StringComparison.Ordinal);
        Assert.Equal("Not done: the user sent a new message before this ran.", results[1]);
        Assert.Empty(_user.Proposals);
        Assert.Empty(_terminal.Typed);
        Assert.Equal(AgentStatus.Idle, agent.Status);
        Assert.False(agent.IsWaitingForUser);
    }

    [Fact]
    public async Task A_message_sent_instead_of_approving_runs_nothing_and_drops_the_plan()
    {
        using var steps = System.Text.Json.JsonDocument.Parse("""[{"command":"apt-get upgrade","purpose":"Update"},{"command":"reboot","purpose":"Restart"}]""");
        List<string>? results = null;
        AgentPlan? planAfter = new("unset", [], string.Empty);
        TerminalAgent? agent = null;
        var client = new ScriptedChatClient(
            _ => Call("propose_plan", Arguments(("summary", "Update"), ("steps", steps.RootElement.Clone()), ("rollback", "None."))),
            _ => Call("run_command", Arguments(("command", "apt-get upgrade"), ("purpose", "Update"), ("plan_step", 1))),
            messages => { results = ToolResults(messages); planAfter = agent!.Plan; return Text("OK, only the logs."); });
        agent = Create(client);
        _user.Decide = proposal => proposal.Kind == ProposalKind.Plan ? CommandApproval.Approve(proposal.Command) : SendInstead<CommandApproval>(agent, "Just show me the logs");

        await agent.SendAsync("Update the server", Token);

        Assert.Contains("Just show me the logs", results![0], StringComparison.Ordinal);
        Assert.Null(planAfter);
        Assert.Empty(_terminal.Typed);
        agent.Dispose();
    }

    [Fact]
    public async Task Takes_no_message_when_nothing_waits_on_the_user()
    {
        using var agent = Create(new ScriptedChatClient(_ => Text("Hi.")));

        Assert.False(agent.Redirect("Hello"));
        await agent.SendAsync("Hello", Token);
        Assert.False(agent.Redirect("Again"));
    }

    [Fact]
    public async Task An_answer_given_as_the_message_is_taken_still_counts_as_the_message()
    {
        string? result = null;
        var client = new ScriptedChatClient(
            _ => Call("ask_user", Arguments(("question", "Which environment?"))),
            messages => { result = ToolResults(messages)[0]; return Text("OK."); });
        using var agent = Create(client);
        _user.Answer = _ =>
        {
            Assert.True(agent.Redirect("Never mind"));
            return "staging";
        };

        await agent.SendAsync("Deploy", Token);

        Assert.EndsWith("Their message:\nNever mind", result, StringComparison.Ordinal);
    }
}
