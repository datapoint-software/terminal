using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// One step of a plan, as the approval card shows it and as the checklist tracks it once the plan is approved.
public sealed partial class PlanStepViewModel : ObservableObject
{
    private static readonly string[] s_connectionWords = ["connection", "session", "reboot", "restart", "disconnect", "ssh"];

    public PlanStepViewModel(int number, AgentPlanStep step, IReadOnlyDictionary<string, string>? environments = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        Number = number;
        Command = step.Command;
        Purpose = step.Purpose;
        Warnings = WarningsFor(step);
        Hosts = HostChipViewModel.For(step.Terminals, environments ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        IsExample = step.IsExample;
        IsReadOnly = CommandClassifier.Classify(step.Command) == CommandRisk.ReadOnly;
        Title = step.Title is { Length: > 0 } title ? title : HasPurpose ? Purpose : Command;
    }

    public int Number { get; }

    public string NumberText => Number.ToString(System.Globalization.CultureInfo.CurrentCulture);

    public string Command { get; }

    public string Purpose { get; }

    public bool HasPurpose => Purpose.Length > 0;

    // The step's name, or what it does, or its command, whichever the agent gave first.
    public string Title { get; }

    // Why the step is needed, when the title doesn't already say it.
    public bool HasWhy => HasPurpose && !string.Equals(Purpose, Title, StringComparison.Ordinal);

    public IReadOnlyList<HostChipViewModel> Hosts { get; }

    public bool HasHosts => Hosts.Count > 0;

    // An example only illustrates the step; the agent may run something a little different, which the step's approval covers.
    public bool IsExample { get; }

    public string CodeLabel => IsExample ? Strings.PlanStepExample : Strings.PlanStepCommand;

    public bool IsReadOnly { get; }

    public string KindText => IsReadOnly ? Strings.PlanStepReadOnly : Strings.PlanStepChanges;

    // Set on a revised plan: the step is new, or differs from the one in the plan before.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChange))]
    public partial string? ChangeText { get; set; }

    public bool HasChange => ChangeText is not null;

    // How many review comments name this step, shown beside it so the user sees where they've commented.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComments), nameof(CommentCountText))]
    public partial int CommentCount { get; set; }

    public bool HasComments => CommentCount > 0;

    public string CommentCountText => CommentCount == 1 ? Strings.PlanOneComment : Strings.Format(Strings.PlanComments, CommentCount);

    public string CommentName => Strings.Format(Strings.PlanCommentOnStep, Number);

    public IReadOnlyList<string> Warnings { get; }

    public bool HasWarnings => Warnings.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsSucceeded), nameof(IsFailed), nameof(IsPending), nameof(IsOver), nameof(StateText), nameof(AccessibleName))]
    public partial PlanStepState State { get; set; }

    // Why a failed step failed, e.g. its exit code.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial string Detail { get; set; } = string.Empty;

    public bool HasDetail => Detail.Length > 0;

    public bool IsPending => State == PlanStepState.Pending;

    public bool IsOver => State is PlanStepState.Succeeded or PlanStepState.Failed or PlanStepState.Finished;

    public void Mark(PlanStepState state, int? exitCode)
    {
        State = state;
        Detail = state == PlanStepState.Failed && exitCode is { } code ? Strings.Format(Strings.CommandExitCode, code) : string.Empty;
    }

    public bool IsRunning => State == PlanStepState.Running;

    public bool IsSucceeded => State is PlanStepState.Succeeded or PlanStepState.Finished;

    public bool IsFailed => State == PlanStepState.Failed;

    public string StateText => State switch
    {
        PlanStepState.Running => Strings.PlanStepRunning,
        PlanStepState.Succeeded => Strings.PlanStepSucceeded,
        PlanStepState.Failed => Strings.PlanStepFailed,
        PlanStepState.Finished => Strings.PlanStepFinished,
        _ => Strings.PlanStepPending,
    };

    public string AccessibleName => Strings.Format(Strings.PlanStepAccessible, Number, StateText, Title);

    public static IReadOnlyList<PlanStepViewModel> For(AgentPlan plan, IReadOnlyDictionary<string, string>? environments = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return [.. plan.Steps.Select((step, index) => new PlanStepViewModel(index + 1, step, environments))];
    }

    // The agent's own warning first, then what the app finds in the command, so a risky step is flagged even when the agent
    // says nothing. The app's "ends the connection" is left out when the agent has already said as much.
    private static List<string> WarningsFor(AgentPlanStep step)
    {
        var warnings = new List<string>();
        if (step.Warning is { } warning)
        {
            warnings.Add(warning);
        }

        var hazards = StepHazardDetector.Detect(step.Command);
        var saidSo = step.Warning is { } said && s_connectionWords.Any(word => said.Contains(word, StringComparison.OrdinalIgnoreCase));
        AddIf(warnings, hazards.HasFlag(StepHazards.EndsConnection) && !saidSo, Strings.PlanHazardEndsConnection);
        AddIf(warnings, hazards.HasFlag(StepHazards.ErasesDisk), Strings.PlanHazardErasesDisk);
        AddIf(warnings, hazards.HasFlag(StepHazards.DeletesRecursively), Strings.PlanHazardDeletesRecursively);
        AddIf(warnings, hazards.HasFlag(StepHazards.ChangesTrust), Strings.PlanHazardChangesTrust);
        return warnings;
    }

    private static void AddIf(List<string> warnings, bool condition, string text)
    {
        if (!condition)
        {
            return;
        }

        warnings.Add(text);
    }
}
