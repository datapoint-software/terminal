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
        _steps = [.. PlanStepViewModel.For(plan)];
        Shown = [.. _steps];
    }

    public string Title { get; }

    private readonly List<PlanStepViewModel> _steps;

    // The plan's steps in its own order, as the agent numbers them.
    public IReadOnlyList<PlanStepViewModel> Steps => _steps;

    // The steps as the checklist lists them: those over first, in the order they ended, then the one being worked on, then
    // the rest in the plan's order, so a step the agent skipped moves down below what it did instead (product decision 122).
    public System.Collections.ObjectModel.ObservableCollection<PlanStepViewModel> Shown { get; }

    private readonly List<PlanStepViewModel> _over = [];

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

    // The step the agent is on: the running one, else the latest it has ended.
    public PlanStepViewModel? Current => _working ?? _over.LastOrDefault();

    // Where the current step is in the list, which may differ from its number in the plan once steps are done out of order.
    private int Position => Current is { } current ? Shown.IndexOf(current) + 1 : 0;

    public string StatusText => (Current, FailedCount) switch
    {
        (null, _) => Strings.Format(Strings.PlanNotStarted, StepCount(Steps.Count)),
        (_, 0) => Strings.Format(Strings.PlanProgress, Position, Steps.Count),
        (_, var failed) => Strings.Format(Strings.PlanProgressFailed, Position, Steps.Count, failed),
    };

    public string FoldedText => IsEnded ? EndedText
        : Current is { } current ? Strings.Format(Strings.PlanFolded, Position, Steps.Count, current.Title)
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

    // The step the agent is working on stays running from its first command until it starts another step or the task ends,
    // so checking a step's result, or thinking about it between commands, still shows as working on it. Its latest finished
    // command's result becomes the step's when it's left; one left before any of its commands finished hasn't been done.
    private PlanStepViewModel? _working;
    private (PlanStepState State, int? ExitCode)? _workingOutcome;

    public void Update(int number, PlanStepState state, int? exitCode)
    {
        if (number < 1 || number > Steps.Count)
        {
            return;
        }

        var step = Steps[number - 1];
        if (state == PlanStepState.Running)
        {
            if (!ReferenceEquals(step, _working))
            {
                LeaveWorkingStep();
                _workingOutcome = null;
            }

            _over.Remove(step);
            _working = step;
            step.Mark(state, exitCode);
        }
        else if (ReferenceEquals(step, _working))
        {
            _workingOutcome = (state, exitCode);
        }
        else
        {
            MarkOver(step, state, exitCode);
        }

        Arrange();
        OnPropertyChanged(string.Empty);
    }

    private void LeaveWorkingStep()
    {
        if (_working is not { } step)
        {
            return;
        }

        _working = null;
        if (_workingOutcome is { } outcome)
        {
            MarkOver(step, outcome.State, outcome.ExitCode);
            return;
        }

        step.Mark(PlanStepState.Pending, exitCode: null);
    }

    private void MarkOver(PlanStepViewModel step, PlanStepState state, int? exitCode)
    {
        step.Mark(state, exitCode);
        _over.Remove(step);
        _over.Add(step);
    }

    // Moves rather than rebuilds the list, so a step the keyboard is on keeps its place in the tab order.
    private void Arrange()
    {
        var order = _over.Concat(_working is { } working ? [working] : []).Concat(Steps.Where(step => !_over.Contains(step) && !ReferenceEquals(step, _working))).ToList();
        for (var index = 0; index < order.Count; index++)
        {
            var from = Shown.IndexOf(order[index]);
            if (from == index)
            {
                continue;
            }

            Shown.Move(from, index);
        }
    }

    // A step the user approved adding goes in at its number; the ones after it move down, as the agent now numbers them.
    public void Insert(int number, AgentPlanStep step)
    {
        if (number < 1 || number > _steps.Count + 1)
        {
            return;
        }

        foreach (var later in _steps.Skip(number - 1))
        {
            later.Number++;
        }

        var added = new PlanStepViewModel(number, step) { ChangeText = Strings.PlanStepAdded };
        _steps.Insert(number - 1, added);
        Shown.Add(added);
        Arrange();
        OnPropertyChanged(string.Empty);
    }

    // The task is over: unpinned and folded to its result, where the plan was approved in the conversation.
    public void End()
    {
        LeaveWorkingStep();
        Arrange();
        IsEnded = true;
        IsPinned = false;
        IsExpanded = false;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}
