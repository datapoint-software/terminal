using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Chat;

/// <summary>
/// The chat beside a terminal: the conversation, the agent's status, and the approvals and questions the agent waits on.
/// The agent calls in from background threads; everything the view sees changes on the user interface thread.
/// </summary>
public sealed partial class ChatViewModel : ObservableObject, IAgentUser, IDisposable
{
    private readonly IUiThread _ui;
    private TerminalAgent? _agent;
    private ChatItemViewModel? _streaming;
    private ChatItemViewModel? _command;

    public ChatViewModel(IUiThread ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        Modes = [new(ApprovalMode.Ask, Strings.ModeAsk), new(ApprovalMode.AskForRisky, Strings.ModeAskForRisky), new(ApprovalMode.Auto, Strings.ModeAuto)];
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
    public partial ApprovalViewModel? Approval { get; set; }

    [ObservableProperty]
    public partial QuestionViewModel? Question { get; set; }

    public bool HasAgent => _agent is not null;

    private bool IsBusy => Status is not (AgentStatus.Idle or AgentStatus.Interrupted);

    /// <summary>Connects the chat to its terminal's agent, and says where the conversation is sent.</summary>
    public void Attach(TerminalAgent agent, string privacyNotice)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agent = agent;
        _agent.Mode = SelectedMode.Mode;
        _agent.EventRaised += OnAgentEvent;
        Status = AgentStatus.Idle;
        StatusText = Strings.StatusIdle;
        Items.Add(new ChatItemViewModel(ChatItemKind.Notice, privacyNotice));
        OnPropertyChanged(nameof(HasAgent));
        NotifyCommands();
    }

    /// <summary>Shows a notice from the application, e.g. that no provider is set up.</summary>
    public void AddNotice(string text) => Items.Add(new ChatItemViewModel(ChatItemKind.Notice, text));

    /// <summary>Called for every keystroke or paste in the terminal.</summary>
    public void OnUserTyped() => _agent?.NotifyUserTyped();

    public Task<CommandApproval> RequestApprovalAsync(CommandProposal proposal, CancellationToken cancellationToken)
    {
        var approval = new ApprovalViewModel(proposal);
        _ui.Post(() => Approval = approval);
        var registration = cancellationToken.Register(approval.Cancel);
        return FinishAsync(approval.Decision, registration, () => ReferenceEquals(Approval, approval), () => Approval = null);
    }

    public Task<string?> AskAsync(AgentQuestion question, CancellationToken cancellationToken)
    {
        var view = new QuestionViewModel(question);
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
    }

    partial void OnSelectedModeChanged(ModeOption value)
    {
        if (_agent is null)
        {
            return;
        }

        _agent.Mode = value.Mode;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private void Send()
    {
        var message = Draft.Trim();
        Draft = string.Empty;
        Items.Add(new ChatItemViewModel(ChatItemKind.User, message));
        Status = AgentStatus.Thinking;
        _ = _agent!.SendAsync(message);
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
    }

    private bool CanSend() => _agent is not null && !IsBusy && !string.IsNullOrWhiteSpace(Draft);

    private bool IsBusyCheck() => _agent is not null && IsBusy;

    private bool CanContinue() => _agent is not null && Status == AgentStatus.Interrupted;

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

    private void Show(AgentEventArgs e)
    {
        switch (e.Kind)
        {
            case AgentEventKind.TextDelta:
                AppendDelta(e.Text);
                break;
            case AgentEventKind.Reply:
                FinishReply(e.Text);
                break;
            case AgentEventKind.CommandStarted:
                _command = new ChatItemViewModel(ChatItemKind.Command, e.Command ?? string.Empty) { Detail = e.Text };
                Items.Add(_command);
                break;
            case AgentEventKind.CommandFinished:
                FinishCommand(e);
                break;
            case AgentEventKind.Notice:
                _streaming = null;
                Items.Add(new ChatItemViewModel(ChatItemKind.Notice, e.Text));
                break;
            case AgentEventKind.StatusChanged:
                SetStatus(e.Status);
                break;
        }
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

    private void SetStatus(AgentStatus status)
    {
        if (status == AgentStatus.Thinking)
        {
            _streaming = null;
        }

        Status = status;
        StatusText = status switch
        {
            AgentStatus.Thinking => Strings.StatusThinking,
            AgentStatus.RunningCommand => Strings.StatusRunningCommand,
            AgentStatus.WaitingForApproval => Strings.StatusWaitingForApproval,
            AgentStatus.WaitingForUser => Strings.StatusWaitingForUser,
            AgentStatus.Interrupted => Strings.StatusInterrupted,
            _ => Strings.StatusIdle,
        };

        // Auto lasts for one task; show the mode the agent fell back to.
        if (status is not AgentStatus.Idle || _agent is null || SelectedMode.Mode == _agent.Mode)
        {
            return;
        }

        SelectedMode = Modes.First(option => option.Mode == _agent.Mode);
    }
}
