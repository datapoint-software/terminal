using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class ChatViewTests
{
    [AvaloniaFact]
    public void Shows_an_approval_without_taking_focus()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.AddNotice("Your messages are sent to Anthropic (api.anthropic.com).");
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Is nginx running?"));
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Command, "systemctl status nginx") { Detail = "Check the service · Exit code 0" });
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Agent, "It's running, but the config has a warning. I'd like to reload it."));
        chat.SetModels([new ModelChoice("a", "claude-opus-5-5", "Opus 5.5", string.Empty), new ModelChoice("a", "claude-sonnet-5-5", "Sonnet 5.5", string.Empty)], new ModelChoice("a", "claude-opus-5-5", "Opus 5.5", string.Empty));
        _ = chat.RequestApprovalAsync(new CommandProposal("sudo systemctl reload nginx", "Reload nginx to apply the fixed config", CommandRisk.Changes, "web-1", "production"), CancellationToken.None);
        var focusTarget = new TextBox();
        var window = new Window
        {
            Width = 420,
            Height = 700,
            Content = new DockPanel { Children = { focusTarget, new ChatView { DataContext = chat } } },
        };
        window.Show();
        focusTarget.Focus();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-approval.png", new PngBitmapEncoderOptions());

        Assert.True(focusTarget.IsFocused);
        Assert.NotNull(frame);
    }

    [AvaloniaFact]
    public void Renders_the_composer_in_dark_mode()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Is nginx running?"));
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Agent, "Yes. **nginx** is `active (running)`."));
        chat.SetModels([new ModelChoice("a", "claude-opus-5-5", "Opus 5.5", string.Empty)], new ModelChoice("a", "claude-opus-5-5", "Opus 5.5", string.Empty));
        var window = new Window { Width = 380, Height = 360, RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-dark.png", new PngBitmapEncoderOptions());

        Assert.NotNull(frame);
    }

    // A long plan scrolls inside its box, so the buttons that approve or decline it stay on screen.
    [AvaloniaFact]
    public void Keeps_the_buttons_in_view_for_a_long_plan()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        var plan = string.Join('\n', Enumerable.Range(1, 60).Select(step => $"{step}. sudo apt-get install --only-upgrade package-{step}"));
        _ = chat.RequestApprovalAsync(new CommandProposal(plan, "Update the server", CommandRisk.Changes, "web-1") { Kind = ProposalKind.Plan }, CancellationToken.None);
        var window = new Window { Width = 420, Height = 700, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-long-plan.png", new PngBitmapEncoderOptions());

        var run = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, chat.Approval!.RunLabel));
        var bottom = run.TranslatePoint(new Point(0, run.Bounds.Height), window)!.Value.Y;
        Assert.InRange(bottom, 0, window.Height);
    }

    [AvaloniaFact]
    public void Shows_the_question_in_bold_and_marks_the_recommended_option()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        var question = new AgentQuestion(
            "How do you want to proceed?",
            [
                new AgentOption("Everything now", "Installs all 6 updates; Docker restarts and so does every container on this node."),
                new AgentOption("Drain the node first", "Moves its tasks to other nodes, updates, then makes it active again.", IsRecommended: true),
                new AgentOption("Stop", "Leaves the server as it is so you can schedule a window."),
            ])
        {
            Details = "Four of the 6 updates are **Docker** packages, and this node is the swarm's only manager.",
        };
        _ = chat.AskAsync(question, CancellationToken.None);
        var window = new Window { Width = 420, Height = 700, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-question.png", new PngBitmapEncoderOptions());

        var options = window.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("option")).ToList();
        Assert.Equal(3, options.Count);
        Assert.Single(options, button => button.Classes.Contains("recommended"));
    }

    [AvaloniaFact]
    public void Shows_a_plans_steps_warnings_and_rollback()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        var plan = new AgentPlan(
            "Drain swarm-a, install the 6 updates, reboot into the new kernel, then make the node active again.",
            [
                new AgentPlanStep("docker node ls", "Check the other nodes are healthy and how many managers there are. Stops and asks if draining would leave too few."),
                new AgentPlanStep("docker node update --availability drain $(docker info -f '{{.Swarm.NodeID}}')", "Move this node's tasks to other nodes"),
                new AgentPlanStep("DEBIAN_FRONTEND=noninteractive apt-get -y --with-new-pkgs upgrade", "Install all updates, including the held-back kernel"),
                new AgentPlanStep("reboot", "Start the new kernel", "Ends the SSH session"),
                new AgentPlanStep("docker node update --availability active $(docker info -f '{{.Swarm.NodeID}}')", "Let the node take work again"),
            ],
            "Run `docker node update --availability active` to bring the node back, and reinstall the previous packages from `/var/cache/apt/archives`.");
        _ = chat.RequestApprovalAsync(
            new CommandProposal(plan.Text, plan.Summary, CommandRisk.Changes, "Local") { Kind = ProposalKind.Plan, Plan = plan with { Title = "Update swarm-a" }, Session = "ssh session to root@swarm-a" },
            CancellationToken.None);
        var window = new Window { Width = 420, Height = 900, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-plan.png", new PngBitmapEncoderOptions());

        Assert.Equal("In Local · ssh session to root@swarm-a", chat.Approval!.TerminalText);
        Assert.Equal("Plan: Update swarm-a", chat.Approval.Heading);
        Assert.Equal("5 steps · 1 ends the SSH session", chat.Approval.StepCountText);
        var reboot = chat.Approval.PlanSteps[3];
        Assert.Equal(["Ends the SSH session"], reboot.Warnings);
        var run = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, chat.Approval.RunLabel));
        Assert.InRange(run.TranslatePoint(new Point(0, run.Bounds.Height), window)!.Value.Y, 0, window.Height);
    }

    [AvaloniaFact]
    public void Pins_an_approved_plan_and_folds_it_into_the_conversation_when_the_task_ends()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        var plan = new AgentPlan(
            "Drain the node, install the updates, reboot, then make it active again.",
            [
                new AgentPlanStep("docker node ls", "Check the swarm is healthy"),
                new AgentPlanStep("docker node update --availability drain web1", "Move this node's tasks away"),
                new AgentPlanStep("apt-get -y upgrade", "Install all updates"),
                new AgentPlanStep("reboot", "Start the new kernel"),
                new AgentPlanStep("docker node update --availability active web1", "Make the node active again"),
            ],
            "None.")
        { Title = "Update swarm-a" };

        chat.Show(new AgentEventArgs(AgentEventKind.PlanApproved, plan.Summary) { Plan = plan, Terminal = "Local" });
        chat.Show(new AgentEventArgs(AgentEventKind.CommandFinished, "Finished") { PlanStep = 1, StepState = PlanStepState.Succeeded, ExitCode = 0 });
        chat.Show(new AgentEventArgs(AgentEventKind.CommandFinished, "Finished") { PlanStep = 2, StepState = PlanStepState.Succeeded, ExitCode = 0 });
        chat.Show(new AgentEventArgs(AgentEventKind.CommandFinished, "Finished") { PlanStep = 3, StepState = PlanStepState.Failed, ExitCode = 100 });
        chat.Show(new AgentEventArgs(AgentEventKind.CommandStarted, "Start the new kernel") { PlanStep = 4, StepState = PlanStepState.Running, Command = "reboot", Terminal = "Local" });
        var window = new Window { Width = 420, Height = 520, Content = new ChatView { DataContext = chat } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/chat-plan-pinned.png", new PngBitmapEncoderOptions());
        }

        var active = chat.ActivePlan!;
        Assert.Equal("Step 4 of 5 · 1 failed", active.StatusText);
        Assert.Equal("Exit code 100", active.Steps[2].Detail);
        Assert.Equal("Step 4 · Local · Start the new kernel", chat.Items[^1].Detail);
        Assert.False(Assert.Single(chat.Items, item => item.IsPlan).Plan!.IsInConversation);

        active.ToggleCommand.Execute(null);
        Assert.Equal("Step 4 of 5 · Start the new kernel", active.HeaderText);

        chat.Show(new AgentEventArgs(AgentEventKind.StatusChanged, string.Empty) { Status = AgentStatus.Idle });
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/chat-plan-ended.png", new PngBitmapEncoderOptions());
        }

        Assert.Null(chat.ActivePlan);
        Assert.True(active.IsInConversation);
        Assert.Equal("Plan stopped · 2 of 5 done, 1 failed", active.HeaderText);
        Assert.Equal("Step 3, failed: Install all updates", active.Steps[2].AccessibleName);
    }
}
