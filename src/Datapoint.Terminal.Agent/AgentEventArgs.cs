namespace Datapoint.Terminal.Agent;

public sealed class AgentEventArgs(AgentEventKind kind, string text) : EventArgs
{
    public AgentEventKind Kind { get; } = kind;

    public string Text { get; } = text;

    public string? Command { get; init; }

    public int? ExitCode { get; init; }

    public AgentStatus Status { get; init; }

    public string? Terminal { get; init; }

    // A finished command's output as the terminal shows it, before redaction.
    public string? Output { get; init; }

    public string? Connection { get; init; }

    public AgentPlan? Plan { get; init; }

    // The approved plan's step a command carries out, and, once it has finished, how it went.
    public int? PlanStep { get; init; }

    public PlanStepState StepState { get; init; }

    public Skills.RolloutProgress? Rollout { get; init; }

    // The file a memory or host documentation change was written to.
    public string? Path { get; init; }
}
