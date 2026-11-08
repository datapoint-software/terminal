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

    public Web.WebAccessKind? WebKind { get; init; }

    public IReadOnlyList<Web.WebSearchResult> Sources { get; init; } = [];

    // Why a search or page read failed; null when it worked.
    public string? Problem { get; init; }

    // Who ran a search.
    public string? Service { get; init; }

    // Text on a page or in search results that addresses an AI model, a likely prompt injection.
    public string? Warning { get; init; }

    // Set by whoever records a finished command in the history, while the event is raised: the entry's number, so the agent
    // can later point the model to it instead of resending its output (product decision 139).
    public long? HistoryId { get; set; }
}
