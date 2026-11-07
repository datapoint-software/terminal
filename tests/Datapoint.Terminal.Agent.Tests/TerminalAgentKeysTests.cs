using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentKeysTests
{
    private readonly FakeUser _user = new();
    private readonly FakeTerminal _terminal = new() { IsAlternateScreen = true, IsAtPrompt = false };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<string?> SendAsync(string keys, ApprovalMode mode)
    {
        string? seen = null;
        var client = new ScriptedChatClient(
            _ => Call("send_keys", new Dictionary<string, object?>(StringComparer.Ordinal) { ["keys"] = keys, ["purpose"] = "save and quit" }),
            messages => { seen = LastToolResult(messages); return Text("OK."); });
        using var agent = new TerminalAgent(client, _user, new SecretRedactor());
        agent.Mode = mode;
        agent.AddTerminal(new AgentTerminal("web1", _terminal));
        await agent.SendAsync("Save the file", Token);
        return seen;
    }

    [Fact]
    public async Task Proposes_keys_before_sending_them()
    {
        var result = await SendAsync("<Esc>:wq<Enter>", ApprovalMode.AskForRisky);

        var proposal = Assert.Single(_user.Proposals);
        Assert.Equal((ProposalKind.SendKeys, "<Esc>:wq<Enter>"), (proposal.Kind, proposal.Command));
        Assert.Equal(["<Esc>:wq<Enter>"], _terminal.Keys, StringComparer.Ordinal);
        Assert.StartsWith("Sent. A full-screen program is open", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sends_without_asking_in_Plan_mode()
    {
        await SendAsync("q", ApprovalMode.Plan);

        Assert.Empty(_user.Proposals);
        Assert.Equal(["q"], _terminal.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Asks_in_Plan_mode_when_the_keys_type_a_command_that_always_asks()
    {
        await SendAsync("<Esc>:!ssh-keygen -R web1<Enter>", ApprovalMode.Plan);

        Assert.Equal(CommandRisk.AlwaysAsk, Assert.Single(_user.Proposals).Risk);
    }

    [Fact]
    public async Task Refuses_keys_at_a_shell_prompt()
    {
        _terminal.IsAlternateScreen = false;
        _terminal.IsAtPrompt = true;

        var result = await SendAsync("rm -rf ~<Enter>", ApprovalMode.Plan);

        Assert.Equal("Not sent: the terminal is at a shell prompt. Use run_command to run commands.", result);
        Assert.Empty(_terminal.Keys);
    }

    [Fact]
    public async Task Does_not_send_declined_keys()
    {
        _user.Decide = proposal => CommandApproval.Reject(proposal.Command, null);

        var result = await SendAsync("q", ApprovalMode.Ask);

        Assert.StartsWith("Not sent: the user declined.", result, StringComparison.Ordinal);
        Assert.Empty(_terminal.Keys);
    }
}
