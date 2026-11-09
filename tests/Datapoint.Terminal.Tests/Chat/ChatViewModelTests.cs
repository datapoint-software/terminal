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
        agent.AddTerminal(new AgentTerminal("Local", new InstantTerminal()));
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

        var first = chat.AskAsync(new AgentQuestion("Which?", [new AgentOption("a"), new AgentOption("b", "The second.")]), Token);
        chat.Question!.ChooseCommand.Execute(chat.Question.Rows[1]);
        Assert.Equal("b", await first);

        // Asked once the first is cleared away, as the agent does; here both would otherwise race on two threads.
        var second = chat.AskAsync(new AgentQuestion("Why?", []), Token);
        Assert.True(chat.Question!.IsWriting);
        chat.Question.Draft = " because ";
        chat.Question.SubmitCommand.Execute(null);

        Assert.Equal("because", await second);
        Assert.Null(chat.Question);
    }

    [Fact]
    public async Task Picks_by_number_or_writes_something_else_and_keeps_the_question_in_the_conversation()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());

        var first = chat.AskAsync(new AgentQuestion("Which?", [new AgentOption("a"), new AgentOption("b")]), Token);
        Assert.Equal<string>(["1 a", "2 b", "3 Something else…"], [.. chat.Question!.Rows.Select(row => $"{row.NumberText} {row.Label}")]);
        Assert.Equal("Press 1–3, or click", chat.Question.KeysText);
        Assert.False(chat.Question.ChooseNumber(4));
        Assert.True(chat.Question.ChooseNumber(3));
        Assert.True(chat.Question.IsWriting);
        Assert.False(chat.Question.ChooseNumber(1));
        chat.Question.BackCommand.Execute(null);
        Assert.False(chat.Question.IsWriting);
        chat.Question.ChooseNumber(3);
        chat.Question.Draft = "c";
        chat.Question.SubmitCommand.Execute(null);
        var second = chat.AskAsync(new AgentQuestion("Why?", []) { Terminal = "Local", Session = "ssh session to root@web1" }, Token);
        Assert.Equal("The agent asks · In Local · ssh session to root@web1", chat.Question!.HeaderText);
        chat.Question.SkipCommand.Execute(null);

        Assert.Equal("c", await first);
        Assert.Null(await second);
        Assert.Equal<string>(
            ["Asked: The agent asked: Which?", "User: c", "Asked: The agent asked: Why? · Skipped"],
            [.. chat.Items.Select(item => $"{item.Kind}: {item.Text}")]);
    }

    [Fact]
    public async Task A_message_sent_while_a_question_waits_replaces_the_task()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        var ask = new ChatResponse(new ChatMessage(
            ChatRole.Assistant,
            [new FunctionCallContent("call-1", "ask_user", new Dictionary<string, object?>(StringComparer.Ordinal) { ["question"] = "Which environment?" })]));
        using var agent = new TerminalAgent(new ListChatClient(ask, new ChatResponse(new ChatMessage(ChatRole.Assistant, "Here are the logs."))), chat, new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("Local", new InstantTerminal()));
        chat.Attach(agent, "privacy notice");
        var sent = new List<string>();
        chat.MessageSent = sent.Add;
        chat.Draft = "Deploy";
        chat.SendCommand.Execute(null);
        await WaitUntilAsync(() => chat.Question is not null);

        Assert.Equal("Or ask for something else instead…", chat.DraftPlaceholder);
        chat.Draft = "Show me the logs instead";
        Assert.True(chat.SendCommand.CanExecute(null));
        chat.SendCommand.Execute(null);
        await WaitUntilAsync(() => chat.Status == AgentStatus.Idle);

        Assert.Null(chat.Question);
        Assert.Equal("Ask the agent…", chat.DraftPlaceholder);
        Assert.Equal<string>(["Deploy", "Show me the logs instead"], [.. sent]);
        Assert.Equal<string>(
            ["User: Deploy", "Asked: The agent asked: Which environment? · You sent a new message instead", "User: Show me the logs instead", "Agent: Here are the logs."],
            [.. chat.Items.Where(item => !item.IsNotice).Select(item => $"{item.Kind}: {item.Text}")]);
    }

    [Fact]
    public void Cannot_send_without_a_provider()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Draft = "hello";

        Assert.False(chat.SendCommand.CanExecute(null));
        Assert.False(chat.HasAgent);
    }

    [Fact]
    public async Task Sends_an_attached_excerpt_once_and_says_so()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        using var agent = new TerminalAgent(new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "A typo."))), chat, new SecretRedactor());
        agent.AddTerminal(new AgentTerminal("Local", new InstantTerminal()));
        chat.Attach(agent, "privacy notice");
        chat.Excerpt = new TerminalExcerpt("Local", "the selected text", "bash: gti: command not found");
        chat.Draft = "Explain this output.";

        Assert.True(chat.HasExcerpt);
        chat.SendCommand.Execute(null);
        await WaitUntilAsync(() => chat.Status == AgentStatus.Idle);

        Assert.Null(chat.Excerpt);
        Assert.Contains("(with the selected text from Local attached)", chat.Items.Single(item => item.Kind == ChatItemKind.User).Text, StringComparison.Ordinal);
    }
}
