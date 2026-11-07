using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Projects;
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
        IgnoredFile = proposal.IgnoredFile;
        PlanText = proposal.IsOutsidePlan ? Strings.ApprovalOutsidePlan
            : proposal.PlanStep is { } step ? Strings.Format(Strings.ApprovalPlanStep, step)
            : null;
        TerminalText = (proposal.Kind, proposal.Environment) switch
        {
            (ProposalKind.Plan, _) when proposal.Terminal.Length == 0 => Strings.ApprovalPlanAnyTerminal,
            (ProposalKind.OpenTerminal, null) => Strings.Format(Strings.ApprovalForConnection, proposal.Terminal),
            (ProposalKind.OpenTerminal, { } environment) => Strings.Format(Strings.ApprovalForConnectionEnvironment, proposal.Terminal, environment),
            (_, null) => Strings.Format(Strings.ApprovalInTerminal, proposal.Terminal),
            (_, { } environment) => Strings.Format(Strings.ApprovalInTerminalEnvironment, proposal.Terminal, environment),
        };
    }

    public ProposalKind Kind { get; }

    public string? IgnoredFile { get; }

    // Opening a terminal names a connection, which isn't the user's to edit; they can decline and say which one instead.
    public bool IsCommandReadOnly => Kind is ProposalKind.OpenTerminal or ProposalKind.Plan or ProposalKind.EditFile;

    public string? PlanText { get; }

    public bool HasPlanText => PlanText is not null;

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
        (ProposalKind.Plan, _, _) => Strings.ApprovalHeadingPlan,
        (ProposalKind.EditFile, _, _) => Strings.ApprovalHeadingEdit,
        (_, not null, _) => Strings.ApprovalHeadingIgnored,
        (_, _, CommandRisk.AlwaysAsk) => Strings.ApprovalHeadingTrust,
        _ => Strings.ApprovalHeading,
    };

    public string RiskText => Kind == ProposalKind.OpenTerminal ? Strings.ApprovalRiskOpen
        : Kind == ProposalKind.Plan ? Strings.ApprovalRiskPlan
        : Kind == ProposalKind.EditFile ? Strings.ApprovalRiskEdit
        : Kind == ProposalKind.SendKeys && Risk != CommandRisk.AlwaysAsk ? Strings.ApprovalRiskKeys
        : IgnoredFile is not null ? Strings.Format(Strings.ApprovalRiskIgnored, ProjectFolders.Display(IgnoredFile))
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

    private bool CanRun() => !string.IsNullOrWhiteSpace(Command);
}
