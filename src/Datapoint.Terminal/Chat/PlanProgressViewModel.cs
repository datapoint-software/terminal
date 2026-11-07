using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// An approved plan as it runs. While the task lasts it's pinned above the conversation; when the task ends it stays, folded
// to its result, where the plan was approved.
public sealed partial class PlanProgressViewModel : ObservableObject
{
    public PlanProgressViewModel(AgentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Title = plan.Title ?? plan.Summary;
        Steps = PlanStepViewModel.For(plan);
    }

    public string Title { get; }

    public IReadOnlyList<PlanStepViewModel> Steps { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInConversation))]
    public partial bool IsPinned { get; set; } = true;

    public bool IsInConversation => !IsPinned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFolded), nameof(HeaderText), nameof(ToggleHelp), nameof(ChevronAngle))]
    public partial bool IsExpanded { get; set; } = true;

    public bool IsFolded => !IsExpanded;

    // The chevron points down to unfold, up to fold.
    public double ChevronAngle => IsExpanded ? 180 : 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderText))]
    public partial bool IsEnded { get; private set; }

    public string HeaderText => IsExpanded ? Title : FoldedText;

    public string ToggleHelp => IsExpanded ? Strings.PlanHideSteps : Strings.PlanShowSteps;

    public double Percent => Steps.Count == 0 ? 0 : 100.0 * Steps.Count(step => step.IsOver) / Steps.Count;

    // The step the agent is on: the running one, else the latest it has reached.
    public PlanStepViewModel? Current =>
        Steps.FirstOrDefault(step => step.IsRunning) ?? Steps.LastOrDefault(step => step.State != PlanStepState.Pending);

    public string StatusText => (Current, FailedCount) switch
    {
        (null, _) => Strings.Format(Strings.PlanNotStarted, StepCount(Steps.Count)),
        ({ } current, 0) => Strings.Format(Strings.PlanProgress, current.Number, Steps.Count),
        ({ } current, var failed) => Strings.Format(Strings.PlanProgressFailed, current.Number, Steps.Count, failed),
    };

    public string FoldedText => IsEnded ? EndedText
        : Current is { } current ? Strings.Format(Strings.PlanFolded, current.Number, Steps.Count, current.Title)
        : StatusText;

    public PlanStepState MarkState => IsEnded
        ? (FailedCount > 0 ? PlanStepState.Failed : DoneCount == Steps.Count ? PlanStepState.Succeeded : PlanStepState.Pending)
        : Current?.State ?? PlanStepState.Pending;

    public bool MarkIsRunning => MarkState == PlanStepState.Running;

    public bool MarkIsSucceeded => MarkState is PlanStepState.Succeeded or PlanStepState.Finished;

    public bool MarkIsFailed => MarkState == PlanStepState.Failed;

    private int FailedCount => Steps.Count(step => step.State == PlanStepState.Failed);

    private int DoneCount => Steps.Count(step => step.State is PlanStepState.Succeeded or PlanStepState.Finished);

    private string EndedText
    {
        get
        {
            var failed = FailedCount;
            var isComplete = Steps.All(step => step.IsOver);
            return (isComplete, failed) switch
            {
                (true, 0) => Strings.Format(Strings.PlanFinished, DoneCount, Steps.Count),
                (true, _) => Strings.Format(Strings.PlanFinishedFailed, DoneCount, Steps.Count, failed),
                (false, 0) => Strings.Format(Strings.PlanStopped, DoneCount, Steps.Count),
                _ => Strings.Format(Strings.PlanStoppedFailed, DoneCount, Steps.Count, failed),
            };
        }
    }

    public static string StepCount(int count) => count == 1 ? Strings.PlanStepCountOne : Strings.Format(Strings.PlanStepCount, count);

    public void Update(int number, PlanStepState state, int? exitCode)
    {
        if (number < 1 || number > Steps.Count)
        {
            return;
        }

        Steps[number - 1].Mark(state, exitCode);
        OnPropertyChanged(string.Empty);
    }

    // The task is over: unpinned and folded to its result, where the plan was approved in the conversation.
    public void End()
    {
        IsEnded = true;
        IsPinned = false;
        IsExpanded = false;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}
