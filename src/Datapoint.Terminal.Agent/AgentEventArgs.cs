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

    // The file a memory or host documentation change was written to.
    public string? Path { get; init; }
}
