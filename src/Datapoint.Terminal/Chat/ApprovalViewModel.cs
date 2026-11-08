using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// The user can edit the command; the agent is told about any edit.
public sealed partial class ApprovalViewModel : ObservableObject
{
    private readonly TaskCompletionSource<CommandApproval> _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ApprovalViewModel(CommandProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        Purpose = proposal.Purpose;
        Risk = proposal.Risk;
        Command = proposal.Command;
        Kind = proposal.Kind;
        PlanSteps = proposal.Plan is { } plan ? PlanStepViewModel.For(plan, proposal.Environments) : [];
        PlanHosts = HostChipViewModel.For(PlanSteps.SelectMany(step => step.Hosts).Select(host => host.Name).Distinct(StringComparer.OrdinalIgnoreCase), proposal.Environments);
        Findings = TextOrNull(proposal.Plan?.Findings);
        AddedStep = proposal.AddedStep;
        IsRevision = proposal.PreviousPlan is not null && AddedStep is null;
        MarkChanges(PlanSteps, proposal.PreviousPlan, AddedStep);
        LookHosts = proposal.Look is { } look ? [.. look.Hosts.Select(host => new LookHostViewModel(host))] : [];
        Rollback = TextOrNull(proposal.Plan?.Rollback);
        PlanTitle = proposal.Plan?.Title;
        StepCountText = StepCount(PlanSteps);
        IgnoredFile = proposal.IgnoredFile;
        CarriedText = proposal.TerminalText;
        IsAfterWeb = proposal.IsAfterWeb;
        AlsoApprovesText = proposal.AlsoApproves.Count == 0 ? null : Strings.Format(Strings.ApprovalAlsoApproves, string.Join(", ", proposal.AlsoApproves));
        PlanText = PlanTextOf(proposal);
        // Inside a session such as ssh, the command runs on that host, not the tab's own machine.
        TerminalText = proposal.Session is { } session ? Strings.Format(Strings.CommandInTerminal, Where(proposal), session) : Where(proposal);
    }

    public ProposalKind Kind { get; }

    // A plan shows its steps and rollback; the command box is for everything else.
    public bool IsPlan => PlanSteps.Count > 0;

    public bool ShowsCommand => !IsPlan && !IsLook && Kind != ProposalKind.PlanMode;

    public IReadOnlyList<PlanStepViewModel> PlanSteps { get; }

    public string? PlanTitle { get; }

    // The plan's page is already about a plan, so its heading is the plan's own name.
    public string DocumentHeading => PlanTitle ?? Strings.ApprovalHeadingPlan;

    // Every host the plan's steps name, in order, for the summary at its top.
    public IReadOnlyList<HostChipViewModel> PlanHosts { get; }

    public bool HasPlanHosts => PlanHosts.Count > 0;

    // What the agent learned while looking, in Markdown.
    public string? Findings { get; }

    public bool HasFindings => Findings is not null;

    // A plan proposed again in the same task, after the user's comments or a decline.
    public bool IsRevision { get; }

    // A step the agent asks to add to the approved plan, by its number in the plan shown (product decision 125).
    public int? AddedStep { get; }

    public bool IsLook => Kind == ProposalKind.Look;

    // A look or a plan has its own buttons, main choice first, and no risk line: what allowing means is in the card itself.
    public bool UsesOwnActions => IsLook || IsPlan;

    // Every other card has a box for the user's reason; a look shows one when the user asks to reply, and a plan is answered
    // with comments on its page.
    public bool ShowsFeedback => !UsesOwnActions || IsReplying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsFeedback))]
    public partial bool IsReplying { get; set; }

    [RelayCommand]
    private void Reply() => IsReplying = true;

    // A look request's heading is the agent's own title, and a plan's summary is on its page, so neither repeats it here.
    public bool ShowsPurpose => !IsLook && !IsPlan;

    public IReadOnlyList<LookHostViewModel> LookHosts { get; }

    // What allowing the look lets the agent do, and for how long.
    public string LookScopeText => LookHosts.Count == 1 ? Strings.ApprovalLookScopeOne : Strings.Format(Strings.ApprovalLookScope, LookHosts.Count);

    // How many steps, and how many need care, e.g. "5 steps · 1 ends the SSH session".
    public string StepCountText { get; }

    public string PlanReadyText => Strings.Format(Strings.PlanReadyText, StepCountText);

    public string? Rollback { get; }

    public bool HasRollback => Rollback is not null;

    public string? IgnoredFile { get; }

    // Why a network command asks: it holds text from the terminals, or web text is in the conversation.
    public string? CarriedText { get; }

    public bool IsAfterWeb { get; }

    // Opening a terminal names a connection, which isn't the user's to edit; they can decline and say which one instead.
    public bool IsCommandReadOnly => Kind is ProposalKind.OpenTerminal or ProposalKind.Plan or ProposalKind.EditFile or ProposalKind.Rollout or ProposalKind.Look or ProposalKind.PlanMode;

    public string? PlanText { get; }

    public bool HasPlanText => PlanText is not null;

    public string? AlsoApprovesText { get; }

    public bool HasAlsoApproves => AlsoApprovesText is not null;

    public string RunLabel => Kind switch
    {
        ProposalKind.OpenTerminal => Strings.ApprovalOpen,
        ProposalKind.SendKeys => Strings.ApprovalSend,
        ProposalKind.Plan => Strings.ApprovalApprove,
        ProposalKind.EditFile => Strings.ApprovalApplyEdit,
        ProposalKind.Rollout => Strings.ApprovalStartRollout,
        ProposalKind.Look => Strings.ApprovalLookAllow,
        ProposalKind.PlanMode => Strings.ApprovalPlanModeSwitch,
        _ => Strings.ApprovalRun,
    };

    public string TerminalText { get; }

    public string Purpose { get; }

    public CommandRisk Risk { get; }

    public string DeclineLabel => IsLook ? Strings.ApprovalLookDecline : Kind == ProposalKind.PlanMode ? Strings.ApprovalPlanModeDecline : Strings.ApprovalDecline;

    public string Heading => (Kind, IgnoredFile, Risk) switch
    {
        (ProposalKind.Look, _, _) => Purpose,
        (ProposalKind.OpenTerminal, _, _) => Strings.ApprovalHeadingOpen,
        (ProposalKind.SendKeys, _, _) => Strings.ApprovalHeadingKeys,
        (ProposalKind.Plan, _, _) => PlanTitle is { } title ? Strings.Format(Strings.PlanReadyHeadingNamed, title) : Strings.PlanReadyHeading,
        (ProposalKind.EditFile, _, _) => Strings.ApprovalHeadingEdit,
        (ProposalKind.Rollout, _, _) => Strings.ApprovalHeadingRollout,
        (ProposalKind.PlanMode, _, _) => Strings.Format(Strings.ApprovalHeadingPlanMode, Command),
        (_, not null, _) => Strings.ApprovalHeadingIgnored,
        (_, _, CommandRisk.AlwaysAsk) => Strings.ApprovalHeadingTrust,
        _ => Strings.ApprovalHeading,
    };

    public string RiskText => KindRiskText() ?? CommandRiskText();

    // What approving this kind of request means, when that doesn't depend on the command.
    private string? KindRiskText() => Kind switch
    {
        ProposalKind.Look => LookScopeText,
        ProposalKind.OpenTerminal => Strings.ApprovalRiskOpen,
        ProposalKind.Plan => Strings.ApprovalRiskPlan,
        ProposalKind.EditFile => Strings.ApprovalRiskEdit,
        ProposalKind.Rollout => Strings.ApprovalRiskRollout,
        ProposalKind.PlanMode => Strings.ApprovalRiskPlanMode,
        ProposalKind.SendKeys when Risk != CommandRisk.AlwaysAsk => Strings.ApprovalRiskKeys,
        _ => null,
    };

    private string CommandRiskText() => IgnoredFile is not null ? Strings.Format(Strings.ApprovalRiskIgnored, WorkspaceFolders.Display(IgnoredFile))
        : CarriedText is not null ? Strings.Format(Strings.ApprovalRiskSendsTerminalText, CarriedText)
        : IsAfterWeb ? Strings.ApprovalRiskAfterWeb
        : Risk switch
    {
        CommandRisk.ReadOnly => Strings.ApprovalRiskReadOnly,
        CommandRisk.AlwaysAsk => Strings.ApprovalRiskAlwaysAsk,
        _ => Strings.ApprovalRiskChanges,
    };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyPropertyChangedFor(nameof(ReadOnlySuggestion), nameof(HasReadOnlySuggestion), nameof(ReadOnlyOfferText))]
    public partial string Command { get; set; }

    // What the user can teach the agent to treat as read-only (product decision 112), from the command as it stands, since
    // the user may edit it; null when there's nothing safe to offer.
    public string? ReadOnlySuggestion => Kind == ProposalKind.RunCommand && Risk == CommandRisk.Changes ? LearnedCommands.Suggest(Command) : null;

    public bool HasReadOnlySuggestion => ReadOnlySuggestion is not null;

    public string? ReadOnlyOfferText => ReadOnlySuggestion is { } suggestion ? Strings.Format(Strings.ApprovalTreatAsReadOnly, suggestion) : null;

    [ObservableProperty]
    public partial bool TreatAsReadOnly { get; set; }

    // Called as the user allows a command the check counted as a change: with what could be taught, and whether they taught it.
    public Action<string, bool>? ReadOnlyApproved { get; set; }

    [ObservableProperty]
    public partial string Feedback { get; set; } = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Completed by the user's decision; nothing waits on a captured context.")]
    public Task<CommandApproval> Decision => _decision.Task;

    public void Cancel() => _decision.TrySetCanceled();

    // Called as the user allows a look, before the agent hears of it, so the Looking card is there for its first command.
    public Action? Allowed { get; set; }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void Run()
    {
        if (!_decision.Task.IsCompleted)
        {
            Allowed?.Invoke();
            if (ReadOnlySuggestion is { } suggestion)
            {
                ReadOnlyApproved?.Invoke(suggestion, TreatAsReadOnly);
            }
        }

        _decision.TrySetResult(CommandApproval.Approve(Command));
    }

    // Opens the plan as a page in the documents area, where the user can read it in full and comment on it.
    public Action<ApprovalViewModel>? ReviewRequested { get; set; }

    [RelayCommand]
    private void Review() => ReviewRequested?.Invoke(this);

    [RelayCommand]
    private void Decline() => _decision.TrySetResult(CommandApproval.Reject(Command, string.IsNullOrWhiteSpace(Feedback) ? null : Feedback.Trim()));

    private static string StepCount(IReadOnlyList<PlanStepViewModel> steps)
    {
        var count = PlanProgressViewModel.StepCount(steps.Count);
        var flagged = steps.Where(step => step.HasWarnings).ToList();
        return flagged.Count switch
        {
            0 => count,
            1 => Strings.Format(Strings.PlanStepCountWarning, count, LowerFirst(flagged[0].Warnings[0])),
            _ => Strings.Format(Strings.PlanStepCountWarnings, count, flagged.Count),
        };
    }

    private static string? PlanTextOf(CommandProposal proposal) => proposal.IsOutsidePlan ? Strings.ApprovalOutsidePlan
        : proposal.PlanStep is { } step ? Strings.Format(Strings.ApprovalPlanStep, step)
        : null;

    private static string? TextOrNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static string Where(CommandProposal proposal) => (proposal.Kind, proposal.Environment) switch
    {
        (ProposalKind.Look, _) => Strings.ApprovalLookWhere,
        (ProposalKind.PlanMode, _) => proposal.Terminal.Length == 0 ? Strings.ApprovalPlanModeAnyHost : Strings.Format(Strings.ApprovalPlanModeHosts, proposal.Terminal),
        (ProposalKind.Plan, _) when proposal.Terminal.Length == 0 => Strings.ApprovalPlanAnyTerminal,
        (ProposalKind.Rollout, null) => Strings.Format(Strings.ApprovalRolloutHosts, proposal.Terminal),
        (ProposalKind.Rollout, { } environment) => Strings.Format(Strings.ApprovalRolloutHostsEnvironment, proposal.Terminal, environment),
        (ProposalKind.OpenTerminal, null) => Strings.Format(Strings.ApprovalForConnection, proposal.Terminal),
        (ProposalKind.OpenTerminal, { } environment) => Strings.Format(Strings.ApprovalForConnectionEnvironment, proposal.Terminal, environment),
        (_, null) => Strings.Format(Strings.ApprovalInTerminal, proposal.Terminal),
        (_, { } environment) => Strings.Format(Strings.ApprovalInTerminalEnvironment, proposal.Terminal, environment),
    };

    // Steps compare by their content, so a step that only moved keeps no mark. A step is changed when one before it had its
    // title, or stood at its number on the same hosts; any other is new.
    // An added step is the only change; the steps after it only moved down.
    private static void MarkChanges(IReadOnlyList<PlanStepViewModel> steps, AgentPlan? previous, int? added)
    {
        if (added is { } number)
        {
            steps.ElementAtOrDefault(number - 1)?.ChangeText = Strings.PlanStepNew;
            return;
        }

        if (previous is null)
        {
            return;
        }

        foreach (var step in steps)
        {
            step.ChangeText = ChangeOf(step, previous);
        }
    }

    private static string? ChangeOf(PlanStepViewModel step, AgentPlan previous)
    {
        var same = previous.Steps.Select((before, index) => new PlanStepViewModel(index + 1, before)).ToList();
        if (same.Any(before => IsSame(before, step)))
        {
            return null;
        }

        var wasThere = same.Any(before => string.Equals(before.Title, step.Title, StringComparison.OrdinalIgnoreCase))
            || (same.ElementAtOrDefault(step.Number - 1) is { } at && IsOnSameHosts(at, step));
        return wasThere ? Strings.PlanStepChanged : Strings.PlanStepNew;
    }

    private static bool IsOnSameHosts(PlanStepViewModel before, PlanStepViewModel after) =>
        before.Hosts.Select(host => host.Name).SequenceEqual(after.Hosts.Select(host => host.Name), StringComparer.OrdinalIgnoreCase);

    private static bool IsSame(PlanStepViewModel before, PlanStepViewModel after) =>
        string.Equals(before.Command, after.Command, StringComparison.Ordinal) && string.Equals(before.Purpose, after.Purpose, StringComparison.Ordinal)
        && string.Equals(before.Title, after.Title, StringComparison.Ordinal) && before.IsExample == after.IsExample && IsOnSameHosts(before, after);

    private static string LowerFirst(string text) =>
        text.Length > 1 && char.IsUpper(text[0]) && !char.IsUpper(text[1]) ? char.ToLower(text[0], System.Globalization.CultureInfo.CurrentCulture) + text[1..] : text;

    private bool CanRun() => !string.IsNullOrWhiteSpace(Command);
}
