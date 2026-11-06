using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
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
    }

    public string Purpose { get; }

    public CommandRisk Risk { get; }

    public string Heading => Risk == CommandRisk.AlwaysAsk ? Strings.ApprovalHeadingTrust : Strings.ApprovalHeading;

    public string RiskText => Risk switch
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
