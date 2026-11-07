using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Chat;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class ChatViewModelModeTests : IDisposable
{
    private readonly ChatViewModel _chat = new(new ImmediateUiThread());
    private readonly TerminalAgent _agent;
    private int _asked;

    public ChatViewModelModeTests()
    {
        _agent = new TerminalAgent(new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK."))), _chat, new SecretRedactor());
        _agent.AddTerminal(new AgentTerminal("Local", new InstantTerminal()));
        _agent.AddTerminal(new AgentTerminal("web1", new InstantTerminal(), "production"));
        _chat.Attach(_agent, "privacy notice");
    }

    public void Dispose()
    {
        _chat.Dispose();
        _agent.Dispose();
    }

    private void Choose(ApprovalMode mode) => _chat.SelectedMode = _chat.Modes.Single(option => option.Mode == mode);

    private void Confirms(bool answer) => _chat.ConfirmIgnorePermissions = () =>
    {
        _asked++;
        return Task.FromResult(answer);
    };

    [Fact]
    public void Offers_the_four_modes_in_order() =>
        Assert.Equal([ApprovalMode.Ask, ApprovalMode.AskForRisky, ApprovalMode.Plan, ApprovalMode.IgnorePermissions], _chat.Modes.Select(option => option.Mode));

    [Fact]
    public void Sets_one_mode_for_the_agent_without_asking_even_with_production_open()
    {
        Confirms(false);

        Choose(ApprovalMode.Plan);

        Assert.Equal(ApprovalMode.Plan, _agent.Mode);
        Assert.Equal(0, _asked);
    }

    [Fact]
    public void Confirms_ignoring_permissions()
    {
        Confirms(true);

        Choose(ApprovalMode.IgnorePermissions);

        Assert.Equal(1, _asked);
        Assert.Equal(ApprovalMode.IgnorePermissions, _agent.Mode);
        Assert.Equal(ApprovalMode.IgnorePermissions, _chat.SelectedMode.Mode);
    }

    [Fact]
    public void Keeps_the_mode_when_ignoring_permissions_is_not_confirmed()
    {
        Confirms(false);
        Choose(ApprovalMode.AskForRisky);

        Choose(ApprovalMode.IgnorePermissions);

        Assert.Equal(ApprovalMode.AskForRisky, _agent.Mode);
        Assert.Equal(ApprovalMode.AskForRisky, _chat.SelectedMode.Mode);
    }

    [Fact]
    public void Shows_the_mode_the_agent_falls_back_to_when_the_conversation_is_cleared()
    {
        Confirms(true);
        Choose(ApprovalMode.Plan);
        Choose(ApprovalMode.IgnorePermissions);

        _agent.ClearConversation();

        Assert.Equal(ApprovalMode.Plan, _chat.SelectedMode.Mode);
    }
}
