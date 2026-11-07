using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using Microsoft.Extensions.AI;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

public sealed class TerminalAgentRolloutTests
{
    private readonly FakeUser _user = new();
    private readonly FakeTerminal _web1 = new();
    private readonly FakeTerminal _web2 = new();
    private readonly FakeTerminal _db1 = new();
    private readonly List<AgentEventArgs> _events = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> Run(string terminal, string command) =>
        _ => Call("run_command", new Dictionary<string, object?>(StringComparer.Ordinal) { ["terminal"] = terminal, ["command"] = command, ["purpose"] = "upgrade" });

    private TerminalAgent Create(params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] steps)
    {
        var agent = new TerminalAgent(new ScriptedChatClient(steps), _user, new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("web1", _web1, "staging"));
        agent.AddTerminal(new AgentTerminal("web2", _web2, "staging"));
        agent.AddTerminal(new AgentTerminal("db1", _db1, "production"));
        agent.EventRaised += (_, e) => _events.Add(e);
        return agent;
    }

    [Fact]
    public async Task One_approval_covers_the_same_command_in_terminals_with_the_same_label()
    {
        using var agent = Create(
            Run("web1", "sudo apt-get upgrade -y nginx"),
            Run("web2", "sudo apt-get upgrade -y nginx"),
            Run("db1", "sudo apt-get upgrade -y nginx"),
            Run("web2", "sudo systemctl restart nginx"),
            _ => Text("Done."));

        await agent.SendAsync("Upgrade nginx everywhere", Token);

        Assert.Equal([("web1", ["web2"]), ("db1", []), ("web2", ["web1"])], _user.Proposals.Select(proposal => (proposal.Terminal, proposal.AlsoApproves.ToArray())));
        Assert.Equal("sudo apt-get upgrade -y nginx", _web2.Typed[0]);
        Assert.Contains(_events, e => e.Kind == AgentEventKind.Notice && string.Equals(e.Text, "Approved in web1 earlier in this task; running the same command in web2.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Carries_the_command_as_the_user_edited_it_and_only_for_the_task()
    {
        _user.Decide = proposal => CommandApproval.Approve(proposal.Command + " --dry-run");
        using var agent = Create(
            Run("web1", "certbot renew"),
            Run("web2", "certbot renew --dry-run"),
            _ => Text("Done."),
            Run("web2", "certbot renew --dry-run"),
            _ => Text("Done."));

        await agent.SendAsync("Renew", Token);
        await agent.SendAsync("And again", Token);

        Assert.Equal(["web1", "web2"], _user.Proposals.Select(proposal => proposal.Terminal), StringComparer.Ordinal);
        Assert.Equal(2, _web2.Typed.Count);
    }

    [Fact]
    public async Task Never_carries_commands_that_always_ask()
    {
        using var agent = Create(Run("web1", "ssh-keygen -R old"), Run("web2", "ssh-keygen -R old"), _ => Text("Done."));

        await agent.SendAsync("Forget the old key", Token);

        Assert.Equal(2, _user.Proposals.Count);
        Assert.All(_user.Proposals, proposal => Assert.Empty(proposal.AlsoApproves));
    }
}
