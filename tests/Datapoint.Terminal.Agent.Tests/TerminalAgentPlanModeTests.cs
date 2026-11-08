using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentPlanModeTests
{
    private static readonly string[] s_hosts = ["web1", "web2"];

    private readonly FakeUser _user = new();
    private readonly List<string?> _results = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Offer() =>
        _ => Call("offer_plan_mode", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = "Upgrade nginx",
            ["hosts"] = s_hosts,
            ["reason"] = "It restarts nginx on two production servers.",
        });

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
        agent.AddTerminal(new AgentTerminal("web1", new FakeTerminal(), "production"));
        await agent.SendAsync("Upgrade nginx on web1 and web2", Token);
        return agent;
    }

    [Fact]
    public async Task Switches_to_Plan_mode_when_the_user_accepts()
    {
        using var agent = await RunAsync(ApprovalMode.Ask, Offer());

        var proposal = Assert.Single(_user.Proposals);
        Assert.Equal((ProposalKind.PlanMode, "Upgrade nginx", "It restarts nginx on two production servers.", "web1, web2"), (proposal.Kind, proposal.Command, proposal.Purpose, proposal.Terminal));
        Assert.Equal(ApprovalMode.Plan, agent.Mode);
        Assert.StartsWith("Switched to Plan mode.", _results[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_mode_and_offers_once_per_task_when_the_user_declines()
    {
        _user.Decide = proposal => CommandApproval.Reject(proposal.Command, "Just do it.");

        using var agent = await RunAsync(ApprovalMode.AskForRisky, Offer(), Offer());

        Assert.Single(_user.Proposals);
        Assert.Equal(ApprovalMode.AskForRisky, agent.Mode);
        Assert.EndsWith("The user said: Just do it.", _results[0], StringComparison.Ordinal);
        Assert.StartsWith("Not offered: you already offered", _results[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Doesnt_offer_in_Plan_mode()
    {
        using var agent = await RunAsync(ApprovalMode.Plan, Offer());

        Assert.Empty(_user.Proposals);
        Assert.StartsWith("Not offered: the approval mode is already Plan.", _results[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Offers_again_in_the_next_task()
    {
        _user.Decide = proposal => CommandApproval.Reject(proposal.Command, null);
        using var agent = await RunAsync(ApprovalMode.Ask, Offer(), _ => Text("Done."), Offer());

        await agent.SendAsync("Now upgrade it on web2", Token);

        Assert.Equal(2, _user.Proposals.Count);
    }
}
