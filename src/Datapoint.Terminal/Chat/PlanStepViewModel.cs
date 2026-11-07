using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// One step of a plan, as the approval card shows it and as the checklist tracks it once the plan is approved.
public sealed partial class PlanStepViewModel : ObservableObject
{
    private static readonly string[] s_connectionWords = ["connection", "session", "reboot", "restart", "disconnect", "ssh"];

    public PlanStepViewModel(int number, AgentPlanStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        Number = number;
        Command = step.Command;
        Purpose = step.Purpose;
        Warnings = WarningsFor(step);
    }

    public int Number { get; }

    public string NumberText => Number.ToString(System.Globalization.CultureInfo.CurrentCulture);

    public string Command { get; }

    public string Purpose { get; }

    public bool HasPurpose => Purpose.Length > 0;

    // The checklist names a step by what it does, or by its command when the agent gave no purpose.
    public string Title => HasPurpose ? Purpose : Command;

    public IReadOnlyList<string> Warnings { get; }

    public bool HasWarnings => Warnings.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph), nameof(IsRunning), nameof(IsSucceeded), nameof(IsFailed), nameof(StateText), nameof(AccessibleName))]
    public partial PlanStepState State { get; set; }

    public bool IsRunning => State == PlanStepState.Running;

    public bool IsSucceeded => State == PlanStepState.Succeeded;

    public bool IsFailed => State == PlanStepState.Failed;

    public string Glyph => State switch
    {
        PlanStepState.Running => "●",
        PlanStepState.Succeeded or PlanStepState.Finished => "✓",
        PlanStepState.Failed => "✕",
        _ => "○",
    };

    public string StateText => State switch
    {
        PlanStepState.Running => Strings.PlanStepRunning,
        PlanStepState.Succeeded => Strings.PlanStepSucceeded,
        PlanStepState.Failed => Strings.PlanStepFailed,
        PlanStepState.Finished => Strings.PlanStepFinished,
        _ => Strings.PlanStepPending,
    };

    public string AccessibleName => Strings.Format(Strings.PlanStepAccessible, Number, StateText, Title);

    public static IReadOnlyList<PlanStepViewModel> For(AgentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return [.. plan.Steps.Select((step, index) => new PlanStepViewModel(index + 1, step))];
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
