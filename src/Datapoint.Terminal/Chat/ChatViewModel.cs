using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
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
    private bool _isSettingModels;

    public ChatViewModel(IUiThread ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        Modes =
        [
            new(ApprovalMode.Ask, Strings.ModeAsk, Strings.ModeAskHelp),
            new(ApprovalMode.AskForRisky, Strings.ModeAskForRisky, Strings.ModeAskForRiskyHelp),
            new(ApprovalMode.Auto, Strings.ModeAuto, Strings.ModeAutoHelp),
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
    public partial ApprovalViewModel? Approval { get; set; }

    [ObservableProperty]
    public partial QuestionViewModel? Question { get; set; }

    public bool HasAgent => _agent is not null && IsConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAgent), nameof(NeedsProvider))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(ContinueCommand))]
    public partial bool IsConnected { get; set; }

    public bool NeedsProvider => !IsConnected;

    public ObservableCollection<ModelChoice> ModelChoices { get; } = [];

    [ObservableProperty]
    public partial ModelChoice? SelectedModel { get; set; }

    public bool HasModelChoices => ModelChoices.Count > 0;

    public Action<ModelChoice>? ModelChosen { get; set; }

    public Action? SettingsRequested { get; set; }

    // For the project's history: each message the user sends, and the conversation being cleared.
    public Action<string>? MessageSent { get; set; }

    public Action? Cleared { get; set; }

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
        Status = AgentStatus.Idle;
        StatusText = Strings.StatusIdle;
        IsConnected = true;
        Items.Add(new ChatItemViewModel(ChatItemKind.Notice, privacyNotice));
        OnPropertyChanged(nameof(HasAgent));
        NotifyCommands();
    }

    public void SetModels(IEnumerable<ModelChoice> choices, ModelChoice? selected)
    {
        ArgumentNullException.ThrowIfNull(choices);
        _isSettingModels = true;
        ModelChoices.Clear();
        foreach (var choice in choices)
        {
            ModelChoices.Add(choice);
        }

        SelectedModel = selected;
        _isSettingModels = false;
        OnPropertyChanged(nameof(HasModelChoices));
    }

    public void AddNotice(string text) => Items.Add(new ChatItemViewModel(ChatItemKind.Notice, text));

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

    // The latest step that ran this command line in this terminal.
    public ChatItemViewModel? FindCommand(string terminal, string commandLine) => Items.LastOrDefault(item =>
        item.IsCommand
        && string.Equals(item.Terminal, terminal, StringComparison.OrdinalIgnoreCase)
        && string.Equals(item.Text.Trim(), commandLine.Trim(), StringComparison.Ordinal));

    public AgentTerminal? CurrentTerminal { get; private set; }

    public void ShowTerminal(AgentTerminal? terminal)
    {
        if (CurrentTerminal is not null)
        {
            CurrentTerminal.ModeChanged -= OnTerminalModeChanged;
        }

        CurrentTerminal = terminal;
        if (terminal is not null)
        {
            terminal.ModeChanged += OnTerminalModeChanged;
        }

        ShowCurrentMode();
        OnPropertyChanged(nameof(CurrentTerminal));
    }

    public void OnUserTyped(AgentTerminal terminal) => _agent?.NotifyUserTyped(terminal);

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

    partial void OnStatusChanged(AgentStatus value)
    {
        OnPropertyChanged(nameof(IsWorking));
        OnPropertyChanged(nameof(NeedsUser));
        OnPropertyChanged(nameof(IsInterrupted));
        OnPropertyChanged(nameof(CanStop));
    }

    partial void OnSelectedModelChanged(ModelChoice? value)
    {
        if (_isSettingModels || value is null)
        {
            return;
        }

        ModelChosen?.Invoke(value);
    }

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke();

    // Product decision 78: Auto on a production terminal is confirmed first; until then the picker shows the mode in force.
    public Func<AgentTerminal, Task<bool>>? ConfirmAuto { get; set; }

    partial void OnSelectedModeChanged(ModeOption value)
    {
        if (CurrentTerminal is not { } terminal || terminal.Mode == value.Mode)
        {
            return;
        }

        if (value.Mode != ApprovalMode.Auto || !EnvironmentLabels.IsProduction(terminal.Environment) || ConfirmAuto is not { } confirm)
        {
            terminal.Mode = value.Mode;
            return;
        }

        _ui.Post(ShowCurrentMode);
        _ = ConfirmAutoAsync(terminal, confirm);
    }

    private static async Task ConfirmAutoAsync(AgentTerminal terminal, Func<AgentTerminal, Task<bool>> confirm)
    {
        if (!await confirm(terminal).ConfigureAwait(true))
        {
            return;
        }

        terminal.Mode = ApprovalMode.Auto;
    }

    private void OnTerminalModeChanged(object? sender, EventArgs e) => _ui.Post(ShowCurrentMode);

    private void ShowCurrentMode()
    {
        if (CurrentTerminal is not { } terminal || SelectedMode.Mode == terminal.Mode)
        {
            return;
        }

        SelectedMode = Modes.First(option => option.Mode == terminal.Mode);
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private void Send()
    {
        var message = Draft.Trim();
        Draft = string.Empty;
        Items.Add(new ChatItemViewModel(ChatItemKind.User, message));
        Status = AgentStatus.Thinking;
        MessageSent?.Invoke(message);
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
        Cleared?.Invoke();
    }

    private bool CanSend() => HasAgent && !IsBusy && !string.IsNullOrWhiteSpace(Draft);

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
                _command = new ChatItemViewModel(ChatItemKind.Command, e.Command ?? string.Empty)
                {
                    Terminal = e.Terminal,
                    Detail = e.Terminal is null ? e.Text : Strings.Format(Strings.CommandInTerminal, e.Terminal, e.Text),
                };
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
    }
}
