using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Chat;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class ChatViewModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ChatResponse Call(string command) => new(new ChatMessage(
        ChatRole.Assistant,
        [new FunctionCallContent("call-1", "run_command", new Dictionary<string, object?>(StringComparer.Ordinal) { ["command"] = command, ["purpose"] = "Check the disk" })]));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10, Token);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Shows_the_conversation_and_the_command_with_its_exit_code()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        using var agent = new TerminalAgent(new ListChatClient(Call("df -h"), new ChatResponse(new ChatMessage(ChatRole.Assistant, "Plenty of space."))), chat, new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("Local", new InstantTerminal(), ApprovalMode.Ask));
        chat.Attach(agent, "privacy notice");
        chat.Draft = "How much disk is left?";
        var sent = new List<string>();
        chat.MessageSent = sent.Add;

        chat.SendCommand.Execute(null);
        await WaitUntilAsync(() => chat.Approval is not null);
        chat.Approval!.RunCommand.Execute(null);
        await WaitUntilAsync(() => chat.Status == AgentStatus.Idle);

        Assert.Equal(
            [ChatItemKind.Notice, ChatItemKind.User, ChatItemKind.Command, ChatItemKind.Agent],
            chat.Items.Select(item => item.Kind));
        Assert.Equal("df -h", chat.Items[2].Text);
        Assert.Equal("Local · Check the disk · Exit code 0", chat.Items[2].Detail);
        Assert.Equal("Plenty of space.", chat.Items[3].Text);
        Assert.Equal(["How much disk is left?"], sent, StringComparer.Ordinal);
        Assert.Null(chat.Approval);
        Assert.Equal(string.Empty, chat.Draft);
    }

    [Fact]
    public async Task Passes_an_edited_command_and_a_decline_reason_to_the_agent()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        var proposal = new CommandProposal("rm -rf build", "Clean", CommandRisk.Changes, "Local");

        var declined = chat.RequestApprovalAsync(proposal, Token);
        chat.Approval!.Command = "rm -rf build/tmp";
        chat.Approval.Feedback = "only tmp";
        chat.Approval.DeclineCommand.Execute(null);
        var answer = await declined;

        Assert.False(answer.Approved);
        Assert.Equal("rm -rf build/tmp", answer.Command);
        Assert.Equal("only tmp", answer.Feedback);
        Assert.Null(chat.Approval);
    }

    [Fact]
    public async Task Removes_the_approval_when_the_agent_is_interrupted()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        using var cancellation = new CancellationTokenSource();
        var pending = chat.RequestApprovalAsync(new CommandProposal("ls", "List", CommandRisk.ReadOnly, "Local"), cancellation.Token);

        await cancellation.CancelAsync();

        await WaitUntilAsync(() => pending.IsCompleted);
        Assert.True(pending.IsCanceled);
        Assert.Null(chat.Approval);
    }

    [Fact]
    public void Cannot_run_an_emptied_command()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        _ = chat.RequestApprovalAsync(new CommandProposal("ls", "List", CommandRisk.ReadOnly, "Local"), Token);

        chat.Approval!.Command = "  ";

        Assert.False(chat.Approval.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task Answers_a_question_with_an_option_or_free_text()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());

        var first = chat.AskAsync(new AgentQuestion("Which?", ["a", "b"]), Token);
        chat.Question!.ChooseCommand.Execute("b");
        var second = chat.AskAsync(new AgentQuestion("Why?", []), Token);
        chat.Question!.Draft = " because ";
        chat.Question.SubmitCommand.Execute(null);

        Assert.Equal("b", await first);
        Assert.Equal("because", await second);
        Assert.Null(chat.Question);
    }

    [Fact]
    public void Changes_the_mode_of_the_terminal_on_show()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        var first = new AgentTerminal("Local", new InstantTerminal(), ApprovalMode.Ask);
        var second = new AgentTerminal("Local 2", new InstantTerminal(), ApprovalMode.Ask);
        chat.ShowTerminal(first);

        chat.SelectedMode = chat.Modes.Single(option => option.Mode == ApprovalMode.AskForRisky);
        chat.ShowTerminal(second);

        Assert.Equal(ApprovalMode.AskForRisky, first.Mode);
        Assert.Equal(ApprovalMode.Ask, second.Mode);
        Assert.Equal(ApprovalMode.Ask, chat.SelectedMode.Mode);
    }

    [Fact]
    public void Shows_a_terminals_mode_when_it_falls_back_after_a_task()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        var terminal = new AgentTerminal("Local", new InstantTerminal(), ApprovalMode.Ask) { Mode = ApprovalMode.Auto };
        chat.ShowTerminal(terminal);

        terminal.Mode = ApprovalMode.Ask;

        Assert.Equal(ApprovalMode.Ask, chat.SelectedMode.Mode);
    }

    [Fact]
    public void Cannot_send_without_a_provider()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Draft = "hello";

        Assert.False(chat.SendCommand.CanExecute(null));
        Assert.False(chat.HasAgent);
    }
}
