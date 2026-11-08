using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Chat;
using Microsoft.Extensions.AI;

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
        chat.ModelName = "Opus 5.5";
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
        Assert.True(window.GetVisualDescendants().OfType<TextBlock>().Single(block => string.Equals(block.Name, "ModelName", StringComparison.Ordinal)).IsEffectivelyVisible);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<ComboBox>(), box => box.ItemCount > 0 && box.Items[0] is not ModeOption);
        Assert.NotNull(frame);
    }

    // Every block in the conversation, messages, commands, notices and cards, sits the same 12 px from the next, as the cards
    // pinned above it do.
    [AvaloniaFact]
    public void Spaces_every_block_of_the_conversation_the_same()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.AddNotice("Your messages are sent to Anthropic (api.anthropic.com).");
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Free some space on the Swarm nodes"));
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Agent, "Checking disk use first."));
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Command, "docker system df") { Detail = "swarm-a · Exit code 0" });
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Command, "df -h /") { Detail = "swarm-a · Exit code 0" });
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Asked, "Remove unused images older than 7 days?"));
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Yes"));
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Agent, "Reclaimed 4.2 GB on swarm-a."));
        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 420, Height = 700, Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/chat-spacing.png", new PngBitmapEncoderOptions());
        }

        var list = view.FindControl<ItemsControl>("ItemList")!;
        var blocks = list.GetRealizedContainers()
            .Select(container => container.GetVisualChildren().OfType<Panel>().Single().Children.First(control => control.IsVisible))
            .Select(block => block.TranslatePoint(new Point(0, 0), list)!.Value.Y is var top ? (Top: top, Bottom: top + block.Bounds.Height) : default)
            .ToList();
        var gaps = blocks.Zip(blocks.Skip(1), (above, below) => Math.Round(below.Top - above.Bottom, 1)).ToList();
        Assert.Equal(Enumerable.Repeat(12.0, blocks.Count - 1), gaps);
        window.Close();
    }

    [AvaloniaFact]
    public void Offers_to_treat_an_approved_command_as_read_only()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Which node leads the Postgres cluster?"));
        _ = chat.RequestApprovalAsync(new CommandProposal("patronictl list --format pretty", "List the cluster's members and its leader", CommandRisk.Changes, "db-1", "production"), CancellationToken.None);
        var window = new Window { Width = 420, Height = 520, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-approval-read-only.png", new PngBitmapEncoderOptions());

        var offer = window.GetVisualDescendants().OfType<CheckBox>().Single(box => box.IsEffectivelyVisible);
        Assert.Equal(Datapoint.Terminal.Resources.Strings.Format(Datapoint.Terminal.Resources.Strings.ApprovalTreatAsReadOnly, "patronictl list"), Avalonia.Automation.AutomationProperties.GetName(offer));
    }

    [AvaloniaFact]
    public void Takes_the_user_to_the_terminal_the_agent_waits_for()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        string? shown = null;
        chat.TerminalShown = name => shown = name;
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Upgrade nginx on web-02"));
        chat.Show(new AgentEventArgs(AgentEventKind.StatusChanged, string.Empty) { Status = AgentStatus.WaitingForUser, Terminal = "web-02" });
        var window = new Window { Width = 420, Height = 520, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-terminal-input.png", new PngBitmapEncoderOptions());

        var go = window.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && Equals(button.Content, "Go to web-02"));
        go.Command!.Execute(null);
        Assert.Equal("web-02", shown);
        chat.Show(new AgentEventArgs(AgentEventKind.StatusChanged, string.Empty) { Status = AgentStatus.Thinking });
        Assert.False(chat.HasInputTerminal);
    }

    [AvaloniaFact]
    public void Shows_no_terminal_card_for_a_question_in_the_chat()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());

        chat.Show(new AgentEventArgs(AgentEventKind.StatusChanged, string.Empty) { Status = AgentStatus.WaitingForUser });

        Assert.False(chat.HasInputTerminal);
    }

    [AvaloniaFact]
    public void Offers_to_plan_a_change_first()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Upgrade nginx on the web servers"));
        _ = chat.RequestApprovalAsync(new CommandProposal("Upgrade nginx", "It restarts nginx on two production servers, one at a time.", CommandRisk.Changes, "web-1, web-2") { Kind = ProposalKind.PlanMode }, CancellationToken.None);
        var window = new Window { Width = 420, Height = 520, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-plan-mode-offer.png", new PngBitmapEncoderOptions());

        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible && Equals(button.Content, Datapoint.Terminal.Resources.Strings.ApprovalPlanModeSwitch));
    }

    [AvaloniaFact]
    public void Renders_the_composer_in_dark_mode()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Is nginx running?"));
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Agent, "Yes. **nginx** is `active (running)`."));
        chat.ModelName = "Opus 5.5";
        var window = new Window { Width = 380, Height = 360, RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-dark.png", new PngBitmapEncoderOptions());

        Assert.NotNull(frame);
    }

    // As the window's main pane the chat can be very wide; the conversation and the composer stay a centred reading column.
    [AvaloniaFact]
    public void Keeps_the_conversation_a_centred_column_in_a_wide_window()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Is anything on web-1 waiting for a restart?"));
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.Agent, "Yes. The kernel and systemd were updated on 7 Nov and web-1 has been up for 23 days, so it's still on the old kernel. Nothing else is waiting."));
        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 1200, Height = 600, Content = view };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-wide.png", new PngBitmapEncoderOptions());

        var list = view.FindControl<ItemsControl>("ItemList")!;
        var left = list.TranslatePoint(new Point(0, 0), window)!.Value.X;
        Assert.Equal(760, list.Bounds.Width);
        Assert.InRange(left + (list.Bounds.Width / 2), 595, 605);
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

        var run = window.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && Equals(button.Content, chat.Approval!.RunLabel));
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
        _ = chat.AskAsync(question with { Terminal = "Local", Session = "ssh session to root@swarm-a" }, CancellationToken.None);

        foreach (var width in new[] { 340, 640 })
        {
            var window = new Window { Width = width, Height = 700, Content = new ChatView { DataContext = chat } };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            Directory.CreateDirectory("rendered");
            frame?.Save($"rendered/chat-question-{width}.png", new PngBitmapEncoderOptions());

            var choices = window.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("choice")).ToList();
            Assert.Equal(4, choices.Count);
            Assert.Single(choices, button => button.Classes.Contains("recommended"));
            Assert.Single(choices, button => button.Classes.Contains("other"));
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Writes_something_else_in_place_and_keeps_the_answered_question()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        _ = chat.AskAsync(new AgentQuestion("How do you want to proceed?", [new AgentOption("Everything now"), new AgentOption("Drain the node first", IsRecommended: true)]), CancellationToken.None);
        var window = new Window { Width = 340, Height = 600, Content = new ChatView { DataContext = chat } };
        window.Show();
        chat.Question!.ChooseNumber(3);
        chat.Question.Draft = "Drain it, but only after 22:00";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/chat-question-writing.png", new PngBitmapEncoderOptions());
        }

        chat.Question.SubmitCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var answered = window.CaptureRenderedFrame();
        answered?.Save("rendered/chat-question-answered.png", new PngBitmapEncoderOptions());

        Assert.Null(chat.Question);
        Assert.Contains(window.GetVisualDescendants().OfType<SelectableTextBlock>(), text => text.IsEffectivelyVisible && string.Equals(text.Text, "The agent asked: How do you want to proceed?", StringComparison.Ordinal));
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
        Assert.Equal("The plan is ready: Update swarm-a", chat.Approval.Heading);
        Assert.Equal("5 steps · 1 ends the SSH session", chat.Approval.StepCountText);
        var reboot = chat.Approval.PlanSteps[3];
        Assert.Equal(["Ends the SSH session"], reboot.Warnings);
        Assert.Equal("5 steps · 1 ends the SSH session. It's open in the documents area: read it, comment on any step, then approve it there.", chat.Approval.PlanReadyText);
        var buttons = window.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => button.Content).ToList();
        Assert.Contains("Show the plan", buttons);
        Assert.DoesNotContain(chat.Approval.RunLabel, buttons);
    }

    [AvaloniaFact]
    public void Shows_looking_on_one_card_with_each_hosts_progress()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "We need to apply the missing updates to node b and c"));
        var look = new AgentLook("Update Swarm nodes b and c", [new AgentLookHost("swarm-b", "Pending updates.", "production") { IsConnection = true }, new AgentLookHost("swarm-c", "Pending updates.", "production") { IsConnection = true }]);
        _ = chat.RequestApprovalAsync(new CommandProposal(look.Text, look.Title, CommandRisk.ReadOnly, string.Empty) { Kind = ProposalKind.Look, Look = look }, CancellationToken.None);
        chat.Approval!.RunCommand.Execute(null);
        chat.Show(new AgentEventArgs(AgentEventKind.Notice, "Opening a terminal for swarm-b: Check pending updates.") { Terminal = "swarm-b" });
        Assert.True(chat.NoteLookHost("swarm-b", null));
        chat.Show(new AgentEventArgs(AgentEventKind.CommandStarted, "Updates") { Terminal = "swarm-b", Command = "apt list --upgradable" });
        chat.Show(new AgentEventArgs(AgentEventKind.CommandFinished, "Updates") { Terminal = "swarm-b", Command = "apt list --upgradable", ExitCode = 0 });
        chat.Show(new AgentEventArgs(AgentEventKind.Notice, "Opening a terminal for swarm-c: Check pending updates.") { Terminal = "swarm-c" });
        Assert.True(chat.NoteLookHost("swarm-c", "Connecting…"));
        chat.Show(new AgentEventArgs(AgentEventKind.StatusChanged, string.Empty) { Status = AgentStatus.RunningCommand, Terminal = "swarm-b" });
        chat.Show(new AgentEventArgs(AgentEventKind.CommandStarted, "Reboot") { Terminal = "swarm-b", Command = "ls /var/run/reboot-required" });
        var window = new Window { Width = 420, Height = 560, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-look-progress.png", new PngBitmapEncoderOptions());

        Assert.DoesNotContain(chat.Items, item => item.Kind == ChatItemKind.Notice);
        Assert.Equal("Looking at swarm-b", chat.StatusText);
        Assert.True(chat.IsLookingAt("swarm-c"));
        Assert.False(chat.IsLookingAt("swarm-a"));
    }

    [AvaloniaFact]
    public void Shows_what_the_agent_is_doing_above_the_message_box_and_the_model_in_the_header()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread()) { ModelName = "Opus 5.5" };
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Which nodes need updates?"));

        chat.Show(new AgentEventArgs(AgentEventKind.StatusChanged, string.Empty) { Status = AgentStatus.Thinking });
        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 420, Height = 300, Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-thinking.png", new PngBitmapEncoderOptions());

        var texts = view.GetLogicalDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && string.Equals(text.Text, chat.StatusText, StringComparison.Ordinal)).ToList();
        var status = Assert.Single(texts);
        Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Polite, Avalonia.Automation.AutomationProperties.GetLiveSetting(status));
        Assert.True(view.FindControl<StackPanel>("ComposerStatus")!.IsEffectivelyVisible);
        Assert.Equal("Opus 5.5", view.FindControl<TextBlock>("ModelName")!.Text);
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
    [Fact]
    public void Lists_the_steps_done_first_in_the_order_they_ended_and_spins_the_one_being_worked_on()
    {
        var progress = new PlanProgressViewModel(new AgentPlan(
            "Check, drain, update, reboot.",
            [
                new AgentPlanStep("docker node ls", "Check the swarm"),
                new AgentPlanStep("docker node update --availability drain web1", "Drain"),
                new AgentPlanStep("apt-get -y upgrade", "Update"),
                new AgentPlanStep("reboot", "Reboot"),
            ],
            "None."));

        progress.Update(1, PlanStepState.Running, null);
        progress.Update(1, PlanStepState.Succeeded, 0);
        progress.Update(3, PlanStepState.Running, null);
        progress.Update(3, PlanStepState.Succeeded, 0);

        // Between its commands the step is still being worked on.
        Assert.True(progress.Steps[2].IsRunning);
        Assert.Equal([1, 3, 2, 4], progress.Shown.Select(step => step.Number));
        Assert.Equal("Step 2 of 4", progress.StatusText);

        progress.Update(2, PlanStepState.Running, null);

        Assert.True(progress.Steps[2].IsSucceeded);
        Assert.Equal([1, 3, 2, 4], progress.Shown.Select(step => step.Number));

        progress.Update(2, PlanStepState.Failed, 1);
        progress.Update(4, PlanStepState.Running, null);
        progress.End();

        // The last step never finished, so it isn't counted as done.
        Assert.True(progress.Steps[3].IsPending);
        Assert.Equal("Plan stopped · 2 of 4 done, 1 failed", progress.HeaderText);
    }
    [AvaloniaFact]
    public void Adds_an_approved_step_to_the_running_plan_in_its_place()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        var plan = new AgentPlan(
            "Install nginx and start it.",
            [new AgentPlanStep("sudo apt-get install -y nginx", "Install it") { Title = "Install nginx" }, new AgentPlanStep("sudo systemctl enable --now nginx", "Start it") { Title = "Start nginx" }],
            "sudo apt-get remove nginx")
        { Title = "Install nginx on web1" };
        var firewall = new AgentPlanStep("sudo ufw allow 80/tcp", "Lets requests reach nginx") { Title = "Open the firewall" };
        var longer = plan with { Steps = [plan.Steps[0], firewall, plan.Steps[1]] };

        chat.Show(new AgentEventArgs(AgentEventKind.PlanApproved, plan.Summary) { Plan = plan, Terminal = "web1" });
        chat.Show(new AgentEventArgs(AgentEventKind.CommandFinished, "Finished") { PlanStep = 1, StepState = PlanStepState.Succeeded, ExitCode = 0 });
        chat.Show(new AgentEventArgs(AgentEventKind.PlanStepAdded, firewall.Title) { Plan = longer, PlanStep = 2 });
        chat.Show(new AgentEventArgs(AgentEventKind.CommandStarted, "Open the firewall") { PlanStep = 2, StepState = PlanStepState.Running, Command = "sudo ufw allow 80/tcp", Terminal = "web1" });
        var window = new Window { Width = 420, Height = 560, Content = new ChatView { DataContext = chat } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/chat-plan-step-added.png", new PngBitmapEncoderOptions());
        }

        var active = chat.ActivePlan!;
        Assert.Equal(["Install nginx", "Open the firewall", "Start nginx"], active.Steps.Select(step => step.Title), StringComparer.Ordinal);
        Assert.Equal([1, 2, 3], active.Steps.Select(step => step.Number));
        Assert.True(active.Steps[1].IsRunning);
        Assert.Equal("Added", active.Steps[1].ChangeText);
        Assert.Equal("Step 2 of 3", active.StatusText);

        // On the review page, the added step is the only one marked, though the ones after it moved.
        var review = new ApprovalViewModel(new CommandProposal(longer.Text, longer.Summary, CommandRisk.Changes, "web1", null) { Kind = ProposalKind.Plan, Plan = longer, PreviousPlan = plan, AddedStep = 2 });
        Assert.Equal([null, "New", null], review.PlanSteps.Select(step => step.ChangeText), StringComparer.Ordinal);
        Assert.False(review.IsRevision);
    }
    // While the conversation is summarized, the ring turns into a spinner that says so, instead of going blank until the next
    // reply reports the new size.
    [AvaloniaFact]
    public async Task Shows_a_spinner_instead_of_the_context_size_while_summarizing()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread()) { ModelName = "Claude Opus 5.5" };
        var gate = new TaskCompletionSource();
        var client = new GatedChatClient(gate.Task,
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "nginx is running.")) { Usage = new UsageDetails { InputTokenCount = 110_000, OutputTokenCount = 500 } },
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "Checked nginx on web1.")),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done.")) { Usage = new UsageDetails { InputTokenCount = 3_000, OutputTokenCount = 100 } });
        using var agent = new TerminalAgent(client, chat, new SecretRedactor());
        chat.Attach(agent, "privacy notice");
        chat.ContextWindow = 1_000_000;
        await agent.SendAsync("Is nginx running?", TestContext.Current.CancellationToken);
        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 420, Height = 300, Content = view };
        window.Show();

        var second = agent.SendAsync("And now?", TestContext.Current.CancellationToken);
        for (var wait = 0; wait < 200 && !chat.IsSummarizing; wait++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(chat.IsSummarizing);
        Assert.Equal("Summarizing…", chat.ContextText);
        var ring = view.FindControl<Button>("ContextUse")!;
        Assert.True(ring.IsEffectivelyVisible);
        Assert.Single(ring.GetVisualDescendants().OfType<Datapoint.Terminal.Components.Spinner>(), spinner => spinner.IsEffectivelyVisible);
        Directory.CreateDirectory("rendered");
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/chat-context-summarizing.png", new PngBitmapEncoderOptions());
        }

        gate.SetResult();
        await second;
        chat.RefreshUsage();
        Assert.False(chat.IsSummarizing);
        Assert.Equal("3k", chat.ContextText);
        window.Close();
    }

    // Answers in order; the second request waits for the gate, so a test can look while it's pending.
    private sealed class GatedChatClient(Task gate, params ChatResponse[] responses) : IChatClient
    {
        private int _next;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var index = _next++;
            if (index == 1)
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var update in responses[Math.Min(index, responses.Length - 1)].ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    // Product decision 133: the summary a long conversation was replaced by is in the chat, folded under the notice.
    [AvaloniaFact]
    public void Shows_the_summary_a_long_conversation_was_replaced_by()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Show(new AgentEventArgs(AgentEventKind.Notice, "The conversation reached 104,512 tokens, so I summarized it and carried on from the summary, to keep each reply cheaper. The chat and the history still have everything.")
        {
            Output = "- Checked **web1**: `/` is 91% full.\n- Rotated the nginx logs; 4 GB freed.\n- Left to do: web2.",
        });
        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 420, Height = 720, Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var expander = view.GetVisualDescendants().OfType<Expander>().Single(item => item.IsEffectivelyVisible);
        Assert.Equal("Summary", expander.Header);
        expander.IsExpanded = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        using var frame = window.CaptureRenderedFrame();
        frame?.Save("rendered/chat-summary-notice.png", new PngBitmapEncoderOptions());
        window.Close();
    }

    [AvaloniaFact]
    public async Task Shows_how_much_of_the_context_the_conversation_fills_until_it_starts_over()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread()) { ModelName = "Claude Opus 5.5" };
        var reply = new ChatResponse(new ChatMessage(ChatRole.Assistant, "nginx is running.")) { Usage = new UsageDetails { InputTokenCount = 230_000, OutputTokenCount = 20_000 } };
        using var agent = new TerminalAgent(new ListChatClient(reply), chat, new SecretRedactor());
        chat.Attach(agent, "privacy notice");
        chat.ContextWindow = 1_000_000;
        await agent.SendAsync("Is nginx running?", TestContext.Current.CancellationToken);
        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 420, Height = 300, Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/chat-context-ring.png", new PngBitmapEncoderOptions());
        }

        // Only the ring and its count show; what the session used and cost opens from it.
        var ring = view.FindControl<Button>("ContextUse")!;
        Assert.True(ring.IsEffectivelyVisible);
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text?.StartsWith("This session", StringComparison.Ordinal) == true);
        Assert.Equal("250k", chat.ContextText);
        Assert.Equal(90.0, chat.ContextSweep, 3);
        ring.Flyout!.ShowAt(ring);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var details = ((Flyout)ring.Flyout).Content as StackPanel;
        Assert.True(details!.IsEffectivelyVisible);
        Assert.Matches(@"^250.000 of 1.000.000 tokens of context used$", chat.ContextDetail);
        Assert.StartsWith("This session", chat.UsageSummary, StringComparison.Ordinal);
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/chat-context-details.png", new PngBitmapEncoderOptions());
        }

        // Starting over keeps the ring, with an estimate until the next reply reports the size (product decision 140).
        agent.ClearConversation();
        chat.RefreshUsage();

        Assert.True(chat.HasContext);
        Assert.True(chat.IsContextEstimated);
        Assert.InRange(chat.ContextUsed, 1_000, 20_000);
        Assert.StartsWith("About ", chat.ContextDetail, StringComparison.Ordinal);
    }

    // Product decision 142: New Conversation starts the conversation's usage over; the session's, which the limit applies to, goes on.
    [AvaloniaFact]
    public async Task Shows_the_conversations_usage_apart_from_the_sessions()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread()) { ModelName = "Claude Opus 5.5" };
        static ChatResponse Reply(string text, long input) =>
            new(new ChatMessage(ChatRole.Assistant, text)) { Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = 1_000 } };
        using var agent = new TerminalAgent(new ListChatClient(Reply("Rotated the logs.", 40_000), Reply("Disk is at 32%.", 9_000)), chat, new SecretRedactor());
        agent.Usage.Price = new Datapoint.Terminal.Agent.Usage.TokenPrice(4m, 20m);
        agent.Usage.Budget = 5m;
        chat.Attach(agent, "privacy notice");
        chat.ContextWindow = 1_000_000;
        await agent.SendAsync("Rotate the logs on the Swarm nodes", TestContext.Current.CancellationToken);

        chat.ClearCommand.Execute(null);
        Assert.StartsWith("This conversation: 0 tokens", chat.ConversationUsage, StringComparison.Ordinal);
        Assert.StartsWith("This session: 41k tokens", chat.UsageSummary, StringComparison.Ordinal);

        await agent.SendAsync("How full is the disk?", TestContext.Current.CancellationToken);
        chat.RefreshUsage();
        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 420, Height = 420, Content = view };
        window.Show();
        var ring = view.FindControl<Button>("ContextUse")!;
        ring.Flyout!.ShowAt(ring);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/chat-usage-conversation.png", new PngBitmapEncoderOptions());
        }

        Assert.StartsWith("This conversation: 10k tokens", chat.ConversationUsage, StringComparison.Ordinal);
        Assert.StartsWith("Requests to the model: 1,", chat.UsageDetail.Replace('.', ','), StringComparison.Ordinal);
        Assert.StartsWith("This session: 51k tokens", chat.UsageSummary, StringComparison.Ordinal);
        Assert.Contains("limit US$5", chat.UsageSummary.Replace(',', '.'), StringComparison.Ordinal);
        window.Close();
    }

    [Theory]
    [InlineData(950, "950")]
    [InlineData(48_400, "48k")]
    [InlineData(1_050_000, "1.1M")]
    public void Shortens_token_counts(long tokens, string expected) =>
        Assert.Equal(expected, ChatViewModel.ShortTokens(tokens).Replace(',', '.'));
}
