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
        PlanSteps = proposal.Plan is { } plan ? PlanStepViewModel.For(plan) : [];
        Rollback = string.IsNullOrWhiteSpace(proposal.Plan?.Rollback) ? null : proposal.Plan.Rollback;
        PlanTitle = proposal.Plan?.Title;
        StepCountText = StepCount(PlanSteps);
        IgnoredFile = proposal.IgnoredFile;
        AlsoApprovesText = proposal.AlsoApproves.Count == 0 ? null : Strings.Format(Strings.ApprovalAlsoApproves, string.Join(", ", proposal.AlsoApproves));
        PlanText = proposal.IsOutsidePlan ? Strings.ApprovalOutsidePlan
            : proposal.PlanStep is { } step ? Strings.Format(Strings.ApprovalPlanStep, step)
            : null;
        var where = (proposal.Kind, proposal.Environment) switch
        {
            (ProposalKind.Plan, _) when proposal.Terminal.Length == 0 => Strings.ApprovalPlanAnyTerminal,
            (ProposalKind.OpenTerminal, null) => Strings.Format(Strings.ApprovalForConnection, proposal.Terminal),
            (ProposalKind.OpenTerminal, { } environment) => Strings.Format(Strings.ApprovalForConnectionEnvironment, proposal.Terminal, environment),
            (_, null) => Strings.Format(Strings.ApprovalInTerminal, proposal.Terminal),
            (_, { } environment) => Strings.Format(Strings.ApprovalInTerminalEnvironment, proposal.Terminal, environment),
        };


        // Inside a session such as ssh, the command runs on that host, not the tab's own machine.
        TerminalText = proposal.Session is { } session ? Strings.Format(Strings.CommandInTerminal, where, session) : where;
    }

    public ProposalKind Kind { get; }

    // A plan shows its steps and rollback; the command box is for everything else.
    public bool IsPlan => PlanSteps.Count > 0;

    public bool ShowsCommand => !IsPlan;

    public IReadOnlyList<PlanStepViewModel> PlanSteps { get; }

    public string? PlanTitle { get; }

    // How many steps, and how many need care, e.g. "5 steps · 1 ends the SSH session".
    public string StepCountText { get; }

    public string? Rollback { get; }

    public bool HasRollback => Rollback is not null;

    public string? IgnoredFile { get; }

    // Opening a terminal names a connection, which isn't the user's to edit; they can decline and say which one instead.
    public bool IsCommandReadOnly => Kind is ProposalKind.OpenTerminal or ProposalKind.Plan or ProposalKind.EditFile;

    public string? PlanText { get; }

    public bool HasPlanText => PlanText is not null;

    public string? AlsoApprovesText { get; }

    public bool HasAlsoApproves => AlsoApprovesText is not null;

    public string RunLabel => Kind switch
    {
        ProposalKind.OpenTerminal => Strings.ApprovalOpen,
        ProposalKind.SendKeys => Strings.ApprovalSend,
        ProposalKind.Plan => Strings.ApprovalApprovePlan,
        ProposalKind.EditFile => Strings.ApprovalApplyEdit,
        _ => Strings.ApprovalRun,
    };

    public string TerminalText { get; }

    public string Purpose { get; }

    public CommandRisk Risk { get; }

    public string Heading => (Kind, IgnoredFile, Risk) switch
    {
        (ProposalKind.OpenTerminal, _, _) => Strings.ApprovalHeadingOpen,
        (ProposalKind.SendKeys, _, _) => Strings.ApprovalHeadingKeys,
        (ProposalKind.Plan, _, _) => PlanTitle is { } title ? Strings.Format(Strings.ApprovalHeadingPlanNamed, title) : Strings.ApprovalHeadingPlan,
        (ProposalKind.EditFile, _, _) => Strings.ApprovalHeadingEdit,
        (_, not null, _) => Strings.ApprovalHeadingIgnored,
        (_, _, CommandRisk.AlwaysAsk) => Strings.ApprovalHeadingTrust,
        _ => Strings.ApprovalHeading,
    };

    public string RiskText => Kind == ProposalKind.OpenTerminal ? Strings.ApprovalRiskOpen
        : Kind == ProposalKind.Plan ? Strings.ApprovalRiskPlan
        : Kind == ProposalKind.EditFile ? Strings.ApprovalRiskEdit
        : Kind == ProposalKind.SendKeys && Risk != CommandRisk.AlwaysAsk ? Strings.ApprovalRiskKeys
        : IgnoredFile is not null ? Strings.Format(Strings.ApprovalRiskIgnored, WorkspaceFolders.Display(IgnoredFile))
        : Risk switch
    {
        CommandRisk.ReadOnly => Strings.ApprovalRiskReadOnly,
        CommandRisk.AlwaysAsk => Strings.ApprovalRiskAlwaysAsk,
        _ => Strings.ApprovalRiskChanges,
    };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial string Command { get; set; }

    [ObservableProperty]
    public partial string Feedback { get; set; } = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Completed by the user's decision; nothing waits on a captured context.")]
    public Task<CommandApproval> Decision => _decision.Task;

    public void Cancel() => _decision.TrySetCanceled();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void Run() => _decision.TrySetResult(CommandApproval.Approve(Command));

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

    private static string LowerFirst(string text) =>
        text.Length > 1 && char.IsUpper(text[0]) && !char.IsUpper(text[1]) ? char.ToLower(text[0], System.Globalization.CultureInfo.CurrentCulture) + text[1..] : text;

    private bool CanRun() => !string.IsNullOrWhiteSpace(Command);
}
