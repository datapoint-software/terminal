using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Usage;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Chat;

// The agent calls in from background threads; everything the view sees changes on the user interface thread.
public sealed partial class ChatViewModel : ObservableObject, IAgentUser, IDisposable
{
    private readonly IUiThread _ui;
    private TerminalAgent? _agent;
    private ChatItemViewModel? _streaming;
    private ChatItemViewModel? _command;
    private LookProgressViewModel? _look;

    public ChatViewModel(IUiThread ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        Modes =
        [
            new(ApprovalMode.Ask, Strings.ModeAsk, Strings.ModeAskHelp),
            new(ApprovalMode.AskForRisky, Strings.ModeAskForRisky, Strings.ModeAskForRiskyHelp),
            new(ApprovalMode.Plan, Strings.ModePlan, Strings.ModePlanHelp),
            new(ApprovalMode.IgnorePermissions, Strings.ModeIgnorePermissions, Strings.ModeIgnorePermissionsHelp),
        ];
        SelectedMode = Modes[0];
        StatusText = Strings.StatusNoProvider;
    }

    public ObservableCollection<ChatItemViewModel> Items { get; } = [];

    public IReadOnlyList<ModeOption> Modes { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Draft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(StopCommand), nameof(ContinueCommand), nameof(ClearCommand))]
    public partial AgentStatus Status { get; set; }

    [ObservableProperty]
    public partial ModeOption SelectedMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingCard), nameof(DraftPlaceholder))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial ApprovalViewModel? Approval { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingCard), nameof(DraftPlaceholder))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial QuestionViewModel? Question { get; set; }

    // While the agent waits on a card, the message box stays open: a message sent then replaces what the agent was doing.
    public bool HasPendingCard => Approval is not null || Question is not null;

    public string DraftPlaceholder => HasPendingCard ? Strings.ChatInputReplaces : Strings.ChatInputPlaceholder;

    public bool HasAgent => _agent is not null && IsConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAgent), nameof(NeedsProvider))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(ContinueCommand))]
    public partial bool IsConnected { get; set; }

    public bool NeedsProvider => !IsConnected;

    // The model the conversation goes to, by its friendly name; chosen in settings (product decision 123).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModelName))]
    public partial string? ModelName { get; set; }

    public bool HasModelName => !string.IsNullOrEmpty(ModelName);

    public Action? SettingsRequested { get; set; }

    // A plan to review in the documents area: when the user asks to, or a revision of one they're already reviewing.
    public Action<ApprovalViewModel, bool>? PlanShown { get; set; }

    // For the workspace's history: each message the user sends, and the conversation being cleared.
    public Action<string>? MessageSent { get; set; }

    public Action? Cleared { get; set; }

    // Product decision 115: the terminal the agent waits for the user to type in, e.g. at a password prompt, named in a card
    // that takes the user there, since the terminals may be collapsed or another one in front.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInputTerminal), nameof(InputTerminalTitle), nameof(InputTerminalShow))]
    [NotifyCanExecuteChangedFor(nameof(ShowInputTerminalCommand))]
    public partial string? InputTerminal { get; private set; }

    public bool HasInputTerminal => InputTerminal is not null;

    public string InputTerminalTitle => Strings.Format(Strings.ChatTerminalInputTitle, InputTerminal ?? string.Empty);

    public string InputTerminalShow => Strings.Format(Strings.ChatTerminalInputShow, InputTerminal ?? string.Empty);

    public Action<string>? TerminalShown { get; set; }

    [RelayCommand(CanExecute = nameof(HasInputTerminal))]
    private void ShowInputTerminal() => TerminalShown?.Invoke(InputTerminal!);

    public bool IsWorking => Status is AgentStatus.Thinking or AgentStatus.RunningCommand;

    public bool NeedsUser => Status is AgentStatus.WaitingForApproval or AgentStatus.WaitingForUser;

    public bool IsInterrupted => Status == AgentStatus.Interrupted;

    public bool CanStop => _agent is not null && IsBusy;

    private bool IsBusy => Status is not (AgentStatus.Idle or AgentStatus.Interrupted);

    public void Attach(TerminalAgent agent, string privacyNotice)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agent = agent;
        _agent.EventRaised += OnAgentEvent;
        _agent.ModeChanged += OnAgentModeChanged;
        ShowCurrentMode();
        Status = AgentStatus.Idle;
        StatusText = Strings.StatusIdle;
        IsConnected = true;
        Items.Add(new ChatItemViewModel(ChatItemKind.Notice, privacyNotice));
        OnPropertyChanged(nameof(HasAgent));
        NotifyCommands();
    }

    public void AddNotice(string text) => Items.Add(new ChatItemViewModel(ChatItemKind.Notice, text));

    // Product decision 117: while the agent looks, how a host's terminal is coming along shows on the Looking card instead
    // of a line in the chat. False when no look covers the host, so the caller adds its notice.
    public bool NoteLookHost(string host, string? state) => _look?.Note(host, state) == true;

    public bool IsLookingAt(string host) => _look?.Covers(host) == true;

    public void AddNotice(string text, string actionLabel, Action action) => Items.Add(new ChatItemViewModel(ChatItemKind.Notice, text)
    {
        ActionLabel = actionLabel,
        ActionCommand = new RelayCommand(action),
    });

    // Product.md, Output sent to the provider: what goes with the next message is said before it's sent.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingOutput))]
    public partial string PendingOutputText { get; private set; } = string.Empty;

    public bool HasPendingOutput => PendingOutputText.Length > 0;

    public void RefreshPendingOutput()
    {
        var pending = _agent is null || IsWorking || NeedsUser ? [] : _agent.PendingOutput();
        if (pending.Count == 0)
        {
            PendingOutputText = string.Empty;
            return;
        }

        var terminals = string.Join(", ", pending.Select(item => Strings.Format(Strings.PendingOutputTerminal, item.Terminal, item.Lines)));
        PendingOutputText = Strings.Format(Strings.PendingOutput, terminals) + (pending.Any(item => item.IsCut) ? " " + Strings.PendingOutputCut : string.Empty);
    }

    // Product decision 49: what this session has cost so far, per terminal in the tooltip.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsage))]
    public partial string UsageSummary { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsageDetail))]
    public partial string UsageDetail { get; private set; } = string.Empty;

    public bool HasUsageDetail => UsageDetail.Length > 0;

    public bool HasUsage => UsageSummary.Length > 0;

    // The model's context window in tokens, when known; the ring beside the usage shows how much of it the conversation fills.
    public int? ContextWindow
    {
        get;
        set
        {
            field = value;
            RefreshUsage();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContext), nameof(ContextSweep), nameof(ContextText), nameof(ContextDetail))]
    public partial long ContextUsed { get; private set; }

    // Shown once a request has used some, even when the window isn't known, since the session's usage opens from it; and while
    // the conversation is summarized, when the size isn't known again until the next reply (product decision 133).
    public bool HasContext => ContextUsed > 0 || IsSummarizing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContext), nameof(ContextText), nameof(ContextDetail), nameof(IsContextKnown))]
    public partial bool IsSummarizing { get; private set; }

    public bool IsContextKnown => !IsSummarizing;

    public double ContextSweep => ContextUsed > 0 && ContextWindow > 0 ? 360.0 * Math.Min(1.0, (double)ContextUsed / ContextWindow!.Value) : 0;

    public string ContextText => IsSummarizing ? Strings.ContextSummarizing : ShortTokens(ContextUsed);

    public string ContextDetail => IsSummarizing ? Strings.ContextSummarizingDetail
        : !HasContext ? string.Empty
        : ContextWindow > 0 ? Strings.Format(Strings.ContextUsedDetail, ContextUsed, ContextWindow!.Value)
        : Strings.Format(Strings.ContextUsedUnknownWindow, ContextUsed);

    internal static string ShortTokens(long tokens) => tokens switch
    {
        < 1_000 => tokens.ToString(System.Globalization.CultureInfo.CurrentCulture),
        < 999_500 => Strings.Format(Strings.TokensThousands, Math.Round(tokens / 1_000.0)),
        _ => Strings.Format(Strings.TokensMillions, tokens / 1_000_000.0),
    };

    public void RefreshUsage()
    {
        var usage = _agent?.Usage;
        ContextUsed = usage?.ContextTokens ?? 0;
        IsSummarizing = _agent?.IsSummarizing == true;
        OnPropertyChanged(nameof(HasContext));
        OnPropertyChanged(nameof(ContextSweep));
        OnPropertyChanged(nameof(ContextDetail));
        if (usage is null || usage.Session.Requests == 0)
        {
            UsageSummary = usage?.Budget is { } limit ? UsageText.Summary(UsageTotal.Empty, limit) : string.Empty;
            UsageDetail = string.Empty;
            return;
        }

        UsageSummary = UsageText.Summary(usage.Session, usage.Budget);
        UsageDetail = string.Join('\n', new[] { UsageText.Breakdown(usage.Session), UsageText.PerTerminal(usage.Terminals) }.Where(line => line.Length > 0));
    }

    // Explain and fix (product.md): part of a terminal that goes with the next message, said before it's sent like new output.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExcerpt), nameof(ExcerptText))]
    public partial TerminalExcerpt? Excerpt { get; set; }

    public bool HasExcerpt => Excerpt is not null;

    public string ExcerptText => Excerpt is { } excerpt ? Strings.Format(Strings.ExcerptAttached, excerpt.Label, excerpt.Terminal, excerpt.Lines) : string.Empty;

    [RelayCommand]
    private void RemoveExcerpt() => Excerpt = null;

    // The latest step that ran this command line in this terminal.
    public ChatItemViewModel? FindCommand(string terminal, string commandLine) => Items.LastOrDefault(item =>
        item.IsCommand
        && string.Equals(item.Terminal, terminal, StringComparison.OrdinalIgnoreCase)
        && string.Equals(item.Text.Trim(), commandLine.Trim(), StringComparison.Ordinal));

    public AgentTerminal? CurrentTerminal { get; private set; }

    public void ShowTerminal(AgentTerminal? terminal)
    {
        CurrentTerminal = terminal;
        OnPropertyChanged(nameof(CurrentTerminal));
    }

    public void OnUserTyped(AgentTerminal terminal) => _agent?.NotifyUserTyped(terminal);

    // Set by the window: what the user approved that the check counted as a change, and whether they taught it as read-only.
    public Action<string, bool>? ReadOnlyApproved { get; set; }

    public Task<CommandApproval> RequestApprovalAsync(CommandProposal proposal, CancellationToken cancellationToken)
    {
        var approval = new ApprovalViewModel(proposal)
        {
            ReviewRequested = plan => PlanShown?.Invoke(plan, true),
            ReadOnlyApproved = (command, taught) => ReadOnlyApproved?.Invoke(command, taught),
        };
        if (proposal.Look is { } look)
        {
            approval.Allowed = () => StartLook(look);
        }

        _ui.Post(() =>
        {
            Approval = approval;
            if (!approval.IsPlan)
            {
                return;
            }

            EndLook();
            PlanShown?.Invoke(approval, false);
        });
        var registration = cancellationToken.Register(approval.Cancel);
        return FinishAsync(approval.Decision, registration, () => ReferenceEquals(Approval, approval), () => Approval = null);
    }

    public Task<string?> AskAsync(AgentQuestion question, CancellationToken cancellationToken)
    {
        var view = new QuestionViewModel(question);
        view.Answered = answer => RecordAnswer(view, answer);
        _ui.Post(() => Question = view);
        var registration = cancellationToken.Register(view.Cancel);
        return FinishAsync(view.Response, registration, () => ReferenceEquals(Question, view), () => Question = null);
    }

    public void Dispose()
    {
        if (_agent is null)
        {
            return;
        }

        _agent.EventRaised -= OnAgentEvent;
        _agent.ModeChanged -= OnAgentModeChanged;
    }

    partial void OnStatusChanged(AgentStatus value)
    {
        OnPropertyChanged(nameof(IsWorking));
        OnPropertyChanged(nameof(NeedsUser));
        OnPropertyChanged(nameof(IsInterrupted));
        OnPropertyChanged(nameof(CanStop));
    }

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke();

    // Product decision 107: Ignore permissions is confirmed each time; until then the picker shows the mode in force.
    public Func<Task<bool>>? ConfirmIgnorePermissions { get; set; }

    partial void OnSelectedModeChanged(ModeOption value)
    {
        if (_agent is not { } agent || agent.Mode == value.Mode)
        {
            return;
        }

        if (value.Mode != ApprovalMode.IgnorePermissions || ConfirmIgnorePermissions is not { } confirm)
        {
            agent.Mode = value.Mode;
            return;
        }

        _ui.Post(ShowCurrentMode);
        _ = ConfirmIgnoreAsync(agent, confirm);
    }

    private static async Task ConfirmIgnoreAsync(TerminalAgent agent, Func<Task<bool>> confirm)
    {
        if (!await confirm().ConfigureAwait(true))
        {
            return;
        }

        agent.Mode = ApprovalMode.IgnorePermissions;
    }

    private void OnAgentModeChanged(object? sender, EventArgs e) => _ui.Post(ShowCurrentMode);

    private void ShowCurrentMode()
    {
        if (_agent is not { } agent || SelectedMode.Mode == agent.Mode)
        {
            return;
        }

        SelectedMode = Modes.First(option => option.Mode == agent.Mode);
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private void Send()
    {
        var message = Draft.Trim();
        var excerpt = Excerpt;
        var replaces = IsBusy;
        if (replaces && !ReplacePending(message, excerpt))
        {
            return;
        }

        Draft = string.Empty;
        Excerpt = null;
        Items.Add(new ChatItemViewModel(ChatItemKind.User, excerpt is null ? message : message + "\n" + Strings.Format(Strings.ExcerptSent, excerpt.Label, excerpt.Terminal)));
        Status = AgentStatus.Thinking;
        MessageSent?.Invoke(message);
        if (replaces)
        {
            return;
        }

        _ = _agent!.SendAsync(message, excerpt);
    }

    // The card goes, a line says what became of it, and the agent takes the message as its new goal in the same task. The
    // draft stays when the agent had stopped waiting a moment before, so nothing the user typed is lost.
    private bool ReplacePending(string message, TerminalExcerpt? excerpt)
    {
        var question = Question;
        if (!HasPendingCard || !_agent!.Redirect(message, excerpt))
        {
            return false;
        }

        Items.Add(question is null
            ? new ChatItemViewModel(ChatItemKind.Notice, Strings.ApprovalReplaced)
            : Asked(question, Strings.QuestionReplaced));
        Question = null;
        Approval = null;
        EndPlan();
        return true;
    }

    // An answered question stays in the conversation: what was asked, then the answer as the user's own message.
    private void RecordAnswer(QuestionViewModel question, string? answer)
    {
        Items.Add(Asked(question, answer is null ? Strings.QuestionSkipped : null));
        if (answer is null)
        {
            return;
        }

        Items.Add(new ChatItemViewModel(ChatItemKind.User, answer));
    }

    private static ChatItemViewModel Asked(QuestionViewModel question, string? outcome)
    {
        var asked = Strings.Format(Strings.QuestionAsked, question.Text);
        return new ChatItemViewModel(ChatItemKind.Asked, outcome is null ? asked : Strings.Format(Strings.CommandInTerminal, asked, outcome));
    }

    [RelayCommand(CanExecute = nameof(IsBusyCheck))]
    private void Stop() => _agent?.Interrupt();

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue()
    {
        Status = AgentStatus.Thinking;
        _ = _agent!.ContinueAsync();
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        _agent!.ClearConversation();
        Items.Clear();
        ActivePlan = null;
        ActiveRollout = null;
        Cleared?.Invoke();
    }

    private bool CanSend() => HasAgent && (!IsBusy || HasPendingCard) && !string.IsNullOrWhiteSpace(Draft);

    private bool IsBusyCheck() => _agent is not null && IsBusy;

    private bool CanContinue() => HasAgent && Status == AgentStatus.Interrupted;

    private bool CanClear() => _agent is not null && !IsBusy;

    private void NotifyCommands()
    {
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Completed by the user's decision; nothing waits on a captured context.")]
    private async Task<T> FinishAsync<T>(Task<T> decision, CancellationTokenRegistration registration, Func<bool> isShown, Action hide)
    {
        try
        {
            return await decision.ConfigureAwait(false);
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);
            _ui.Post(() => HideIfShown(isShown, hide));
        }
    }

    private static void HideIfShown(Func<bool> isShown, Action hide)
    {
        if (!isShown())
        {
            return;
        }

        hide();
    }

    private void OnAgentEvent(object? sender, AgentEventArgs e) => _ui.Post(() => Show(e));

    internal void Show(AgentEventArgs e)
    {
        switch (e.Kind)
        {
            case AgentEventKind.TextDelta:
                AppendDelta(e.Text);
                break;
            case AgentEventKind.Reply:
                FinishReply(e.Text);
                break;
            case AgentEventKind.PlanApproved or AgentEventKind.PlanStepAdded:
                ShowPlan(e);
                break;
            case AgentEventKind.RolloutChanged:
                ShowRollout(e);
                break;
            case AgentEventKind.CommandStarted or AgentEventKind.CommandFinished:
                MarkPlanStep(e);
                ShowCommand(e);
                break;
            case AgentEventKind.Notice:
                ShowNotice(e);
                break;
            case AgentEventKind.StatusChanged:
                ChangeStatus(e);
                break;
            case AgentEventKind.UsageChanged:
                RefreshUsage();
                break;
            case AgentEventKind.WebUsed:
                _streaming = null;
                Items.Add(WebItem(e));
                break;
        }
    }

    private void ShowCommand(AgentEventArgs e)
    {
        if (IsLooking(e))
        {
            return;
        }

        if (e.Kind == AgentEventKind.CommandFinished)
        {
            FinishCommand(e);
            return;
        }

        _command = new ChatItemViewModel(ChatItemKind.Command, e.Command ?? string.Empty)
        {
            Terminal = e.Terminal,
            Detail = StepTag(e, e.Terminal is null ? e.Text : Strings.Format(Strings.CommandInTerminal, e.Terminal, e.Text)),
        };
        Items.Add(_command);
    }

    // The plan in progress, pinned above the conversation until the task ends.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivePlan))]
    public partial PlanProgressViewModel? ActivePlan { get; private set; }

    public bool HasActivePlan => ActivePlan is not null;

    // A planned command says which step it is, so it can be matched with the pinned plan.
    private static string StepTag(AgentEventArgs e, string detail) =>
        e.PlanStep is { } step ? Strings.Format(Strings.CommandInTerminal, Strings.Format(Strings.PlanStepTag, step), detail) : detail;

    private void AddPlanStep(AgentEventArgs e)
    {
        if (e.Plan is not { } plan || e.PlanStep is not { } number || number > plan.Steps.Count)
        {
            return;
        }

        ActivePlan?.Insert(number, plan.Steps[number - 1]);
    }

    private void ShowPlan(AgentEventArgs e)
    {
        if (e.Kind == AgentEventKind.PlanStepAdded)
        {
            AddPlanStep(e);
            return;
        }

        _streaming = null;
        if (e.Plan is not { } plan)
        {
            return;
        }

        EndPlan();
        var progress = new PlanProgressViewModel(plan);
        Items.Add(new ChatItemViewModel(ChatItemKind.Plan, plan.Summary) { Terminal = e.Terminal, Plan = progress });
        ActivePlan = progress;
    }

    // A skill's rollout, pinned above the conversation while it runs; it can outlast the task that started it.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveRollout))]
    public partial RolloutProgressViewModel? ActiveRollout { get; private set; }

    public bool HasActiveRollout => ActiveRollout is not null;

    private void ShowRollout(AgentEventArgs e)
    {
        if (e.Rollout is not { } progress)
        {
            return;
        }

        if (ActiveRollout is { } active && active.Id == progress.Id)
        {
            active.Update(progress);
        }
        else
        {
            _streaming = null;
            ActiveRollout = new RolloutProgressViewModel(progress, isPaused => _agent?.PauseRollout(isPaused), () => _agent?.StopRollout());
            Items.Add(new ChatItemViewModel(ChatItemKind.Rollout, progress.Title) { Rollout = ActiveRollout });
        }

        if (!progress.IsOver)
        {
            return;
        }

        ActiveRollout = null;
    }

    private void MarkPlanStep(AgentEventArgs e)
    {
        if (e.PlanStep is not { } number)
        {
            return;
        }

        ActivePlan?.Update(number, e.StepState, e.ExitCode);
    }

    private void EndPlan()
    {
        ActivePlan?.End();
        ActivePlan = null;
    }

    private void AppendDelta(string text)
    {
        if (_streaming is null)
        {
            _streaming = new ChatItemViewModel(ChatItemKind.Agent, string.Empty);
            Items.Add(_streaming);
        }

        _streaming.Text += text;
    }

    private void FinishReply(string text)
    {
        if (_streaming is null)
        {
            Items.Add(new ChatItemViewModel(ChatItemKind.Agent, text));
        }
        else
        {
            _streaming.Text = text;
        }

        _streaming = null;
    }

    private void FinishCommand(AgentEventArgs e)
    {
        _streaming = null;
        if (_command is null)
        {
            return;
        }

        var outcome = e.ExitCode is { } code ? Strings.Format(Strings.CommandExitCode, code) : e.Text;
        _command.Detail = string.IsNullOrEmpty(_command.Detail) ? outcome : $"{_command.Detail} · {outcome}";
        _command = null;
    }

    // While the agent looks, its commands on the hosts it asked about go on the Looking card rather than a line each.
    private bool IsLooking(AgentEventArgs e)
    {
        if (_look is not { } look || e.Terminal is not { } terminal || e.Command is not { } command)
        {
            return false;
        }

        return e.Kind == AgentEventKind.CommandStarted ? look.Start(terminal, command) : look.Finish(terminal, command, e.ExitCode);
    }

    private void StartLook(AgentLook look)
    {
        EndLook();
        _streaming = null;
        _look = new LookProgressViewModel(look);
        Items.Add(new ChatItemViewModel(ChatItemKind.Look, look.Title) { Look = _look });
    }

    private void EndLook()
    {
        _look?.End();
        _look = null;
    }

    // The terminal is known before the status changes, so whatever reacts to the agent now needing the user can name it.
    private void ChangeStatus(AgentEventArgs e)
    {
        InputTerminal = e.Status == AgentStatus.WaitingForUser ? e.Terminal : null;
        SetStatus(e.Status, e.Terminal);
    }

    private void ShowNotice(AgentEventArgs e)
    {
        _streaming = null;
        if (e.Terminal is { } terminal && NoteLookHost(terminal, Strings.LookOpening))
        {
            return;
        }

        Items.Add(e.Output is { Length: > 0 } detail
            ? new ChatItemViewModel(ChatItemKind.Notice, e.Text) { Detail = detail, DetailLabel = Strings.NoticeSummaryLabel }
            : new ChatItemViewModel(ChatItemKind.Notice, e.Text));
    }

    private void SetStatus(AgentStatus status, string? terminal = null)
    {
        if (status == AgentStatus.Thinking)
        {
            _streaming = null;
        }

        // The plan lasts for the task, which is over once the agent stops or is interrupted.
        if (status is AgentStatus.Idle or AgentStatus.Interrupted)
        {
            EndPlan();
            EndLook();
        }

        Status = status;
        StatusText = status switch
        {
            AgentStatus.Thinking => Strings.StatusThinking,
            AgentStatus.RunningCommand when terminal is not null && IsLookingAt(terminal) => Strings.Format(Strings.StatusLookingAt, terminal),
            AgentStatus.RunningCommand => Strings.StatusRunningCommand,
            AgentStatus.WaitingForApproval => Strings.StatusWaitingForApproval,
            AgentStatus.WaitingForUser => Strings.StatusWaitingForUser,
            AgentStatus.Interrupted => Strings.StatusInterrupted,
            _ => Strings.StatusIdle,
        };
    }
}
