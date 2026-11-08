using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Skills;
using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class RolloutCardTests
{
    private static readonly Guid s_id = Guid.NewGuid();
    private static readonly DateTimeOffset s_start = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private static readonly SkillRollout s_rollout = new()
    {
        HealthCheck = "systemctl is-system-running && curl -fsS localhost/health",
        HealthyFor = TimeSpan.FromMinutes(2),
        OnFailure = RolloutFailure.Ask,
    };

    private static RolloutProgress Progress(RolloutHost web2, RolloutOutcome outcome = RolloutOutcome.Running, bool isPaused = false) =>
        new(s_id, "patch-and-reboot", "Patch and reboot", s_rollout,
        [
            new RolloutHost("web-1", RolloutHostState.Done) { StartedAt = s_start, EndedAt = s_start.AddMinutes(6), Note = "healthy for 2m" },
            web2,
            new RolloutHost("web-3", outcome == RolloutOutcome.Running ? RolloutHostState.Waiting : RolloutHostState.Skipped),
        ])
        { Batch = 2, BatchCount = 3, Outcome = outcome, IsPaused = isPaused };

    private static readonly RolloutHost s_checking = new("web-2", RolloutHostState.Checking) { StartedAt = s_start.AddMinutes(6), HealthyFor = TimeSpan.FromSeconds(80) };

    [AvaloniaFact]
    public void Pins_a_rollout_with_each_hosts_state()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "Run Patch and reboot on web-1, web-2 and web-3."));
        chat.Show(new AgentEventArgs(AgentEventKind.RolloutChanged, "Patch and reboot") { Rollout = Progress(s_checking) });
        var window = new Window { Width = 420, Height = 640, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-rollout.png", new PngBitmapEncoderOptions());

        var card = chat.ActiveRollout!;
        Assert.Equal("Patch and reboot · 3 hosts, one at a time", card.Title);
        Assert.Equal("Done · 6 min|Checking health|Next", string.Join("|", card.Hosts.Select(host => host.StateText)));
        Assert.Equal("Healthy for 1:20 of 2:00", card.Hosts[1].Detail);
        Assert.Equal("Healthy for 2m", card.Hosts[0].Detail);
        Assert.Equal("Pause After This Host", card.PauseText);
        Assert.False(card.IsInConversation);
    }

    [Fact]
    public void Names_what_a_pause_waits_for()
    {
        var batches = Progress(s_checking) with { Rollout = s_rollout with { Order = RolloutOrder.Batches, BatchSize = 2 } };

        Assert.Equal("Pause After This Host", new RolloutProgressViewModel(Progress(s_checking), _ => { }, () => { }).PauseText);
        Assert.Equal("Pause After This Batch", new RolloutProgressViewModel(batches, _ => { }, () => { }).PauseText);
    }

    [AvaloniaFact]
    public void Pauses_and_stops_through_the_agent()
    {
        var paused = new List<bool>();
        var stops = 0;
        var card = new RolloutProgressViewModel(Progress(s_checking), paused.Add, () => stops++);

        card.PauseCommand.Execute(null);
        card.Update(Progress(s_checking, isPaused: true));
        card.PauseCommand.Execute(null);
        card.StopCommand.Execute(null);

        Assert.Equal([true, false], paused);
        Assert.Equal(1, stops);
        Assert.Equal("Resume", card.PauseText);
        Assert.Equal("Paused after web-2", card.StatusText);
    }

    [AvaloniaFact]
    public void Folds_into_the_conversation_once_it_is_over()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Show(new AgentEventArgs(AgentEventKind.RolloutChanged, "Patch and reboot") { Rollout = Progress(s_checking) });
        var card = chat.ActiveRollout!;

        var failed = new RolloutHost("web-2", RolloutHostState.Failed) { Note = "the health check kept failing for 15m", FailedCheck = true };
        chat.Show(new AgentEventArgs(AgentEventKind.RolloutChanged, "Patch and reboot") { Rollout = Progress(failed, RolloutOutcome.Failed) });

        Assert.Null(chat.ActiveRollout);
        Assert.True(card.IsInConversation);
        Assert.Equal("Patch and reboot: stopped after a failure, 1 of 3 hosts done", card.HeaderText);
        Assert.Equal("Done · 6 min|Failed|Skipped", string.Join("|", card.Hosts.Select(host => host.StateText)));
        Assert.Same(card, Assert.Single(chat.Items).Rollout);
    }

    [AvaloniaFact]
    public void Asks_to_approve_a_rollout_with_its_hosts_in_order()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        _ = chat.RequestApprovalAsync(
            new CommandProposal("1. web-1\n2. web-2\n\nAfter each, `systemctl is-active nginx` must pass for 2m, within 15m. If a host fails: ask you.", "Patch and reboot: Installs updates and reboots.", CommandRisk.Changes, "web-1, web-2", "production") { Kind = ProposalKind.Rollout },
            CancellationToken.None);

        Assert.Equal("The agent proposes this rollout", chat.Approval!.Heading);
        Assert.Equal("On web-1, web-2 (production)", chat.Approval.TerminalText);
        Assert.Equal("Start Rollout", chat.Approval.RunLabel);
        Assert.True(chat.Approval.IsCommandReadOnly);
    }
}
