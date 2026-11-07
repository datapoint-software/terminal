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

public sealed class PlanReviewTests
{
    private static readonly AgentPlan s_plan = new(
        "Update web1 and web2 one at a time.",
        [
            new AgentPlanStep("systemctl status nginx", "Make sure nginx is healthy before draining.") { Title = "Check nginx", Terminals = ["web1", "web2"] },
            new AgentPlanStep("apt-get install --only-upgrade nginx", "Install the security fix.", "Restarts nginx briefly.") { Title = "Upgrade nginx", Terminals = ["web1"], IsExample = true },
        ],
        "Reinstall the previous package with apt-get install nginx=1.24.0.")
    {
        Title = "Upgrade nginx",
        Findings = "- **web1** runs nginx 1.24.0\n- **web2** is behind the same load balancer",
    };

    private static readonly Dictionary<string, string> s_environments = new(StringComparer.OrdinalIgnoreCase) { ["web1"] = "production", ["web2"] = "production" };

    private static ApprovalViewModel Propose(AgentPlan plan, AgentPlan? previous = null) =>
        new(new CommandProposal(plan.Text, plan.Summary, CommandRisk.Changes, string.Empty) { Kind = ProposalKind.Plan, Plan = plan, Environments = s_environments, PreviousPlan = previous });

    [Fact]
    public void A_look_request_lists_each_host_and_why_and_cannot_be_edited()
    {
        var look = new AgentLook("Look at the web tier", [new AgentLookHost("web1", "Which nginx version runs there.", "production"), new AgentLookHost("db1", "Free disk space.") { IsConnection = true }]);
        var approval = new ApprovalViewModel(new CommandProposal(look.Text, look.Title, CommandRisk.ReadOnly, string.Empty) { Kind = ProposalKind.Look, Look = look });

        Assert.True(approval.IsLook);
        Assert.True(approval.IsCommandReadOnly);
        Assert.False(approval.ShowsCommand);
        Assert.Equal((Strings.ApprovalLookAllow, Strings.ApprovalLookDecline, Strings.ApprovalHeadingLook), (approval.RunLabel, approval.DeclineLabel, approval.Heading));
        Assert.Equal(Strings.Format(Strings.ApprovalLookScope, 2), approval.RiskText);
        Assert.Equal(["web1", "db1"], approval.LookHosts.Select(host => host.Chip.Name).ToList(), StringComparer.Ordinal);
        Assert.True(approval.LookHosts[0].Chip.IsProduction);
        Assert.True(approval.LookHosts[1].OpensConnection);
    }

    [Fact]
    public void A_plan_names_its_hosts_and_marks_examples_and_read_only_steps()
    {
        var approval = Propose(s_plan);

        Assert.Equal(["web1", "web2"], approval.PlanHosts.Select(host => host.Name).ToList(), StringComparer.Ordinal);
        Assert.All(approval.PlanHosts, host => Assert.True(host.IsProduction));
        Assert.Equal(("Check nginx", Strings.PlanStepCommand, Strings.PlanStepReadOnly), (approval.PlanSteps[0].Title, approval.PlanSteps[0].CodeLabel, approval.PlanSteps[0].KindText));
        Assert.Equal((Strings.PlanStepExample, Strings.PlanStepChanges), (approval.PlanSteps[1].CodeLabel, approval.PlanSteps[1].KindText));
        Assert.True(approval.HasFindings);
        Assert.False(approval.IsRevision);
    }

    [Fact]
    public async Task Sending_comments_sends_the_plan_back_with_each_comment_and_what_it_quotes()
    {
        var approval = Propose(s_plan);
        var review = new PlanReviewViewModel(approval, _ => { });
        var comment = review.AddComment("--only-upgrade", 2);
        comment.Text = "Pin the version instead.";
        comment.SaveCommand.Execute(null);
        var general = review.AddComment(null, null);
        general.Text = "Do web2 first.";
        general.SaveCommand.Execute(null);

        Assert.True(review.IsUnsaved);
        Assert.Equal(1, approval.PlanSteps[1].CommentCount);
        review.SendCommentsCommand.Execute(null);
        var decision = await approval.Decision;

        Assert.False(decision.Approved);
        Assert.Equal(
            Strings.PlanFeedbackIntro + "\n- On step 2, about \"--only-upgrade\": Pin the version instead.\n- On the plan: Do web2 first.",
            decision.Feedback);
        Assert.All(review.Comments, item => Assert.True(item.IsSent));
        Assert.False(review.IsUnsaved);
    }

    [Fact]
    public async Task Approving_with_unsent_comments_asks_first()
    {
        var approval = Propose(s_plan);
        var review = new PlanReviewViewModel(approval, _ => { });
        var comment = review.AddComment(null, 1);
        comment.Text = "Also check the config.";
        comment.SaveCommand.Execute(null);

        review.ApproveCommand.Execute(null);
        Assert.True(review.IsConfirmingApprove);
        Assert.False(approval.Decision.IsCompleted);

        review.ApproveAnywayCommand.Execute(null);
        Assert.True((await approval.Decision).Approved);
    }

    [Fact]
    public void A_draft_cancelled_before_saving_goes_away()
    {
        var review = new PlanReviewViewModel(Propose(s_plan), _ => { });
        var comment = review.AddComment("nginx", 1);

        comment.CancelCommand.Execute(null);

        Assert.Empty(review.Comments);
    }

    [Fact]
    public void A_revision_keeps_the_sent_comments_and_marks_what_changed()
    {
        var review = new PlanReviewViewModel(Propose(s_plan), _ => { });
        var comment = review.AddComment(null, 2);
        comment.Text = "Pin the version.";
        comment.SaveCommand.Execute(null);
        review.SendCommentsCommand.Execute(null);
        var revised = s_plan with
        {
            Steps =
            [
                s_plan.Steps[0],
                new AgentPlanStep("apt-get install nginx=1.26.1-1", "Install the security fix.", "Restarts nginx briefly.") { Title = "Upgrade nginx", Terminals = ["web1"] },
                new AgentPlanStep("curl -fsS http://localhost/health", "Check the site answers.") { Title = "Check the site", Terminals = ["web1"] },
            ],
        };

        review.Load(Propose(revised, previous: s_plan));

        Assert.Equal([null, Strings.PlanStepChanged, Strings.PlanStepNew], review.Plan.PlanSteps.Select(step => step.ChangeText).ToList());
        Assert.Equal(Strings.Format(Strings.PlanRevisedMany, "2, 3"), review.RevisionText);
        Assert.Equal((PlanReviewState.Waiting, Strings.PlanStatusRevised), (review.State, review.StatusText));
        Assert.True(Assert.Single(review.Comments).IsSent);
    }

    [AvaloniaFact]
    public void Shows_the_plan_with_its_steps_and_comments()
    {
        var review = new PlanReviewViewModel(Propose(s_plan), _ => { });
        var comment = review.AddComment("--only-upgrade", 2);
        comment.Text = "Pin the version instead.";
        comment.SaveCommand.Execute(null);
        var window = new Window { Width = 1100, Height = 900, Content = new PlanReviewView { DataContext = review } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        window.CaptureRenderedFrame()?.Save("rendered/plan-review.png", new PngBitmapEncoderOptions());

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        Assert.Contains("Check nginx", texts, StringComparer.Ordinal);
        Assert.Contains(Strings.PlanStepExample, texts, StringComparer.Ordinal);
        Assert.Contains(Strings.PlanOneCommentToSend, texts, StringComparer.Ordinal);
        window.Close();
    }
}
