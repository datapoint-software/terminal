using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class ChatViewModelModeTests
{
    private static (ChatViewModel Chat, AgentTerminal Terminal) Create(string? environment, bool confirms, List<string> asked)
    {
        var chat = new ChatViewModel(new ImmediateUiThread())
        {
            ConfirmAuto = terminal => { asked.Add(terminal.Name); return Task.FromResult(confirms); },
        };
        var terminal = new AgentTerminal("web1", new InstantTerminal(), ApprovalMode.Ask, environment);
        chat.ShowTerminal(terminal);
        return (chat, terminal);
    }

    [Fact]
    public void Confirms_Auto_on_a_production_terminal()
    {
        var asked = new List<string>();
        var (chat, terminal) = Create("production", confirms: true, asked);
        using var _ = chat;

        chat.SelectedMode = chat.Modes.Single(option => option.Mode == ApprovalMode.Auto);

        Assert.Equal(["web1"], asked, StringComparer.Ordinal);
        Assert.Equal(ApprovalMode.Auto, terminal.Mode);
    }

    [Fact]
    public void Keeps_the_mode_when_Auto_is_not_confirmed()
    {
        var asked = new List<string>();
        var (chat, terminal) = Create("prod-eu", confirms: false, asked);
        using var _ = chat;

        chat.SelectedMode = chat.Modes.Single(option => option.Mode == ApprovalMode.Auto);

        Assert.Equal(ApprovalMode.Ask, terminal.Mode);
        Assert.Equal(ApprovalMode.Ask, chat.SelectedMode.Mode);
    }

    [Fact]
    public void Does_not_ask_elsewhere()
    {
        var asked = new List<string>();
        var (chat, terminal) = Create("staging", confirms: false, asked);
        using var _ = chat;

        chat.SelectedMode = chat.Modes.Single(option => option.Mode == ApprovalMode.Auto);

        Assert.Empty(asked);
        Assert.Equal(ApprovalMode.Auto, terminal.Mode);
    }
}
