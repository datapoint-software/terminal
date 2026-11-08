using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Tests.Chat;

// The example is the one the plan review was designed with (product decision 111), so the renders can be compared with it.
public sealed class PlanReviewTests
{
    private const string A = "swarm-a";
    private const string B = "swarm-b";
    private const string C = "swarm-c";
    private const string Request = "Drain swarm-b so I can update its kernel tonight, then bring it back.";

    private static readonly AgentLook s_look = new(
        "Before I plan this, I'd like to look at the cluster",
        [
            new AgentLookHost(A, "Which services run on b, and which are pinned to it or keep data on its local volumes, since those can't move when it drains.", "production"),
            new AgentLookHost(B, "Whether b leads any Postgres cluster, and its kernel version, so the plan can move the leader first and target the right update.", "production"),
            new AgentLookHost(C, "Free memory and disk, so I know whether a and c can take b's tasks while it's away.", "production"),
        ]);

    private static readonly AgentPlanStep s_kernel = new(
        "sudo apt-get update\nsudo apt-get install --only-upgrade linux-image-generic\nsudo reboot",
        "b runs 6.8.0-45; 6.8.0-51 is available from the Ubuntu archive.",
        "Ends the SSH session; b is unreachable for 1 to 2 minutes.")
    { Title = "Update the kernel and restart b", Terminals = [B], IsExample = true };

    private static readonly AgentPlanStep s_check = new(
        "docker node ls                                   # on swarm-a\ndocker exec datapoint_postgres-1 patronictl list  # on each node",
        "Confirms every node is Ready and both Postgres clusters have a leader and replicas in sync.")
    { Title = "Check the clusters are healthy", Terminals = [A, B, C] };

    private static readonly AgentPlan s_plan = new(
        "Move b's work to a and c, update its kernel, and bring it back, with no Postgres cluster losing its leader unannounced.",
        [
            new AgentPlanStep(
                "docker exec datapoint_postgres-1 patronictl switchover \\\n  --leader swarm-b --candidate swarm-a --force",
                "b leads the datapoint cluster. Draining it would cut the leader off and force an unplanned failover; a switchover hands over cleanly in a few seconds.",
                "Clients of datapoint reconnect once, for about 5 seconds.")
            { Title = "Move the Postgres leader off b", Terminals = [A] },
            new AgentPlanStep(
                "docker node update --availability drain swarm-b",
                "Stops new tasks on b and moves its 14 running tasks to a and c, which have 21 GB and 18 GB of memory free.",
                "nbg-reports has one replica and is down while it moves, about 30 seconds.")
            { Title = "Drain b", Terminals = [A] },
            new AgentPlanStep("docker node ps swarm-b --filter desired-state=running", "The kernel update reboots b, so every task must have moved first.")
            { Title = "Wait until nothing runs on b", Terminals = [A] },
            s_kernel,
            new AgentPlanStep("docker node update --availability active swarm-b", "Lets the swarm schedule tasks on b again. Existing tasks stay where they are until they restart.")
            { Title = "Bring b back", Terminals = [A] },
            s_check,
        ],
        "Bring b back at any point with `docker node update --availability active swarm-b` on swarm-a. If b doesn't boot the new kernel, choose the previous one (6.8.0-45) in GRUB from the provider's console; the cluster keeps running on a and c meanwhile.")
    {
        Title = "Drain swarm-b for a kernel update",
        Findings = "- `swarm-b` runs 14 tasks across 9 services. None is pinned to b or keeps data on its local volumes.\n"
            + "- `swarm-b` leads the `datapoint` Postgres cluster; `olharpoente` is led by `swarm-c`.\n"
            + "- `swarm-a` and `swarm-c` have 21 GB and 18 GB of memory free; b's tasks use about 9 GB.\n"
            + "- `swarm-b` runs kernel 6.8.0-45.",
    };

    private static readonly AgentPlan s_revised = s_plan with
    {
        Steps =
        [
            .. s_plan.Steps.Take(3),
            s_kernel with
            {
                Title = "Install the pinned kernel and restart b",
                Purpose = "You asked to stay on the kernel pinned in the workspace instructions (6.8.0-49), not the newest.",
                Command = "sudo apt-get update\nsudo apt-get install linux-image-6.8.0-49-generic\nsudo reboot",
                IsExample = false,
            },
            s_plan.Steps[4],
            s_check with
            {
                Purpose = "Confirms every node is Ready, b runs 6.8.0-49, and both Postgres clusters have a leader and replicas in sync.",
                Command = "docker node ls                                   # on swarm-a\nuname -r                                         # on swarm-b\ndocker exec datapoint_postgres-1 patronictl list  # on each node",
            },
        ],
    };

    private static readonly Dictionary<string, string> s_environments = new(StringComparer.OrdinalIgnoreCase) { [A] = "production", [B] = "production", [C] = "production" };

    private static CommandProposal LookProposal => new(s_look.Text, s_look.Title, CommandRisk.ReadOnly, string.Empty) { Kind = ProposalKind.Look, Look = s_look };

    private static CommandProposal PlanProposal(AgentPlan plan, AgentPlan? previous = null) =>
        new(plan.Text, plan.Summary, CommandRisk.Changes, string.Empty) { Kind = ProposalKind.Plan, Plan = plan, Environments = s_environments, PreviousPlan = previous };

    private static ApprovalViewModel Propose(AgentPlan plan, AgentPlan? previous = null) => new(PlanProposal(plan, previous));

    private static PlanCommentViewModel Comment(PlanReviewViewModel review, string? quote, int? step, string text)
    {
        var comment = review.AddComment(quote, step);
        comment.Text = text;
        comment.SaveCommand.Execute(null);
        return comment;
    }

    private static void Render(Control content, double width, double height, string name)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        window.CaptureRenderedFrame()?.Save($"rendered/{name}.png", new PngBitmapEncoderOptions());
        window.Close();
    }

    private static ChatViewModel Chat()
    {
        var chat = new ChatViewModel(new ImmediateUiThread()) { IsConnected = true };
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, Request));
        return chat;
    }

    private static void Run(ChatViewModel chat, string terminal, string command, int? exitCode = 0)
    {
        chat.Show(new AgentEventArgs(AgentEventKind.CommandStarted, string.Empty) { Terminal = terminal, Command = command });
        if (exitCode is null)
        {
            return;
        }

        chat.Show(new AgentEventArgs(AgentEventKind.CommandFinished, string.Empty) { Terminal = terminal, Command = command, ExitCode = exitCode });
    }

    [Fact]
    public void A_look_request_lists_each_host_and_why_and_cannot_be_edited()
    {
        var approval = new ApprovalViewModel(LookProposal);

        Assert.True(approval.IsLook);
        Assert.True(approval.IsCommandReadOnly);
        Assert.False(approval.ShowsCommand);
        Assert.False(approval.ShowsFeedback);
        Assert.Equal((Strings.ApprovalLookAllow, Strings.ApprovalLookDecline, s_look.Title), (approval.RunLabel, approval.DeclineLabel, approval.Heading));
        Assert.Equal(Strings.Format(Strings.ApprovalLookScope, 3), approval.LookScopeText);
        Assert.Equal([A, B, C], approval.LookHosts.Select(host => host.Chip.Name).ToList(), StringComparer.Ordinal);
        Assert.True(approval.LookHosts[0].Chip.IsProduction);

        approval.ReplyCommand.Execute(null);
        Assert.True(approval.ShowsFeedback);
    }

    [Fact]
    public void Commands_on_the_hosts_looked_at_gather_on_the_looking_card()
    {
        using var chat = Chat();
        _ = chat.RequestApprovalAsync(LookProposal, TestContext.Current.CancellationToken);
        chat.Approval!.RunCommand.Execute(null);
        Run(chat, A, "docker node ls");
        Run(chat, B, "uname -r", exitCode: null);
        Run(chat, "Local", "ls");

        var look = Assert.Single(chat.Items, item => item.IsLook).Look!;
        Assert.Equal(Strings.Format(Strings.LookRunning, Strings.Format(Strings.LookHosts, 3)), look.Title);
        Assert.True(look.Hosts[0].Commands.Single().IsDone);
        Assert.True(look.Hosts[1].Commands.Single().IsRunning);
        Assert.Single(chat.Items, item => item.IsCommand);

        chat.Show(new AgentEventArgs(AgentEventKind.StatusChanged, string.Empty) { Status = AgentStatus.Idle });
        Assert.Equal(Strings.Format(Strings.LookDone, Strings.Format(Strings.LookHosts, 3), 2), look.Title);
        Assert.False(look.Hosts[1].Commands.Single().IsRunning);
    }

    [Fact]
    public void A_plan_names_its_hosts_and_marks_examples_and_read_only_steps()
    {
        var approval = Propose(s_plan);

        Assert.Equal([A, B, C], approval.PlanHosts.Select(host => host.Name).ToList(), StringComparer.Ordinal);
        Assert.All(approval.PlanHosts, host => Assert.True(host.IsProduction));
        Assert.Equal(("Wait until nothing runs on b", Strings.PlanStepCommand, Strings.PlanStepReadOnly), (approval.PlanSteps[2].Title, approval.PlanSteps[2].CodeLabel, approval.PlanSteps[2].KindText));
        Assert.Equal((Strings.PlanStepExample, Strings.PlanStepChanges), (approval.PlanSteps[3].CodeLabel, approval.PlanSteps[3].KindText));
        Assert.True(approval.HasFindings);
        Assert.False(approval.IsRevision);
    }

    [Fact]
    public async Task Sending_comments_sends_the_plan_back_with_each_comment_and_what_it_quotes()
    {
        var approval = Propose(s_plan);
        var review = new PlanReviewViewModel(approval, _ => { });
        Comment(review, "--only-upgrade linux-image-generic", 4, "Stay on the pinned kernel from our instructions.");
        var whole = Comment(review, null, 6, "Also check b's kernel.");
        Comment(review, null, null, "Do it after 22:00.");

        Assert.True(review.IsUnsaved);
        Assert.Equal(1, approval.PlanSteps[3].CommentCount);
        Assert.Equal(Strings.Format(Strings.PlanCommentQuoteStep, 6, "Check the clusters are healthy"), whole.DisplayQuote);
        review.SendCommentsCommand.Execute(null);
        var decision = await approval.Decision;

        Assert.False(decision.Approved);
        Assert.Equal(
            Strings.PlanFeedbackIntro + "\n- On step 4, about \"--only-upgrade linux-image-generic\": Stay on the pinned kernel from our instructions."
            + "\n- On step 6: Also check b's kernel.\n- On the plan: Do it after 22:00.",
            decision.Feedback);
        Assert.All(review.Comments, item => Assert.True(item.IsSent));
        Assert.False(review.IsUnsaved);
    }

    [Fact]
    public async Task Approving_with_unsent_comments_asks_first()
    {
        var approval = Propose(s_plan);
        var review = new PlanReviewViewModel(approval, _ => { });
        Comment(review, null, 1, "Check the replicas first.");

        review.ApproveCommand.Execute(null);
        Assert.True(review.IsConfirmingApprove);
        Assert.False(approval.Decision.IsCompleted);

        review.ApproveAnywayCommand.Execute(null);
        Assert.True((await approval.Decision).Approved);
    }

    [Fact]
    public async Task Rejecting_declines_the_plan_with_the_comments_as_reasons()
    {
        var approval = Propose(s_plan);
        var review = new PlanReviewViewModel(approval, _ => { });
        Comment(review, null, null, "The kernel can wait until next month.");

        review.RejectCommand.Execute(null);
        var decision = await approval.Decision;
        await Task.Yield();

        Assert.False(decision.Approved);
        Assert.Equal(Strings.PlanRejectFeedback + "\n" + Strings.PlanRejectComments + "\n- On the plan: The kernel can wait until next month.", decision.Feedback);
        Assert.False(review.IsUnsaved);
        Assert.False(review.RejectCommand.CanExecute(null));
    }

    [Fact]
    public void A_draft_cancelled_before_saving_goes_away()
    {
        var review = new PlanReviewViewModel(Propose(s_plan), _ => { });
        var comment = review.AddComment("patronictl", 1);

        comment.CancelCommand.Execute(null);

        Assert.Empty(review.Comments);
    }

    [Fact]
    public void A_revision_keeps_the_sent_comments_and_marks_what_changed()
    {
        var review = new PlanReviewViewModel(Propose(s_plan), _ => { });
        Comment(review, "--only-upgrade linux-image-generic", 4, "Stay on the pinned kernel.");
        review.SendCommentsCommand.Execute(null);

        review.Load(Propose(s_revised, previous: s_plan));

        Assert.Equal([null, null, null, Strings.PlanStepChanged, null, Strings.PlanStepChanged], review.Plan.PlanSteps.Select(step => step.ChangeText).ToList());
        Assert.Equal(Strings.Format(Strings.PlanRevisedMany, "4, 6"), review.RevisionText);
        Assert.Equal((PlanReviewState.Waiting, Strings.PlanStatusRevised), (review.State, review.StatusText));
        Assert.True(Assert.Single(review.Comments).IsSent);
    }

    // Stage 1 of the design: the request to look.
    [AvaloniaFact]
    public void Shows_the_request_to_look()
    {
        using var chat = Chat();
        _ = chat.RequestApprovalAsync(LookProposal, TestContext.Current.CancellationToken);

        Render(new ChatView { DataContext = chat }, 360, 760, "plan-review-1-ask");

        chat.Approval!.Cancel();
    }

    // Stage 2: looking, with the commands run so far.
    [AvaloniaFact]
    public void Shows_what_the_agent_runs_while_looking()
    {
        using var chat = Chat();
        _ = chat.RequestApprovalAsync(LookProposal, TestContext.Current.CancellationToken);
        chat.Approval!.RunCommand.Execute(null);
        Run(chat, A, "docker node ls");
        Run(chat, A, "docker node ps swarm-b");
        Run(chat, A, "docker service inspect --format '{{.Spec.TaskTemplate.Placement}}' datapoint_postgres");
        Run(chat, A, "docker service ls", exitCode: null);
        Run(chat, B, "uname -r");

        Render(new ChatView { DataContext = chat }, 360, 760, "plan-review-2-looking");
    }

    // Stage 3: the plan's card in the chat, and its page with a comment.
    [AvaloniaFact]
    public void Shows_the_plan_to_review()
    {
        using var chat = Chat();
        _ = chat.RequestApprovalAsync(LookProposal, TestContext.Current.CancellationToken);
        chat.Approval!.RunCommand.Execute(null);
        foreach (var (terminal, command) in new[] { (A, "docker node ls"), (A, "docker node ps swarm-b"), (B, "uname -r"), (C, "free -h") })
        {
            Run(chat, terminal, command);
        }

        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Agent, "b leads the datapoint Postgres cluster, so the plan hands that over first. Everything else on b can move."));
        _ = chat.RequestApprovalAsync(PlanProposal(s_plan), TestContext.Current.CancellationToken);
        Render(new ChatView { DataContext = chat }, 360, 760, "plan-review-3-chat");

        var review = new PlanReviewViewModel(chat.Approval!, _ => { });
        Comment(review, "sudo apt-get install --only-upgrade linux-image-generic", 4, "Stay on the pinned kernel from our instructions, not the newest.");
        Render(new PlanReviewView { DataContext = review }, 750, 2300, "plan-review-3-document");
        Assert.Equal(1, review.PendingCount);
        chat.Approval!.Cancel();
    }

    // Stage 4: the revised plan, with what changed marked and the comment sent.
    [AvaloniaFact]
    public void Shows_the_revised_plan()
    {
        var review = new PlanReviewViewModel(Propose(s_plan), _ => { });
        Comment(review, "sudo apt-get install --only-upgrade linux-image-generic", 4, "Stay on the pinned kernel from our instructions, not the newest.");
        review.SendCommentsCommand.Execute(null);
        review.Load(Propose(s_revised, previous: s_plan));

        var view = new PlanReviewView { DataContext = review };
        Render(view, 750, 2300, "plan-review-4-revised");

        var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        Assert.Contains("Install the pinned kernel and restart b", texts, StringComparer.Ordinal);
        Assert.Contains(Strings.PlanStepChanged, texts, StringComparer.Ordinal);
    }
}
