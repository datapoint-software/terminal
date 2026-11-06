namespace Datapoint.Terminal.Agent;

/// <summary>Something the chat pane shows. Raised on a background thread.</summary>
public sealed class AgentEventArgs(AgentEventKind kind, string text) : EventArgs
{
    public AgentEventKind Kind { get; } = kind;

    /// <summary>The reply text, notice, or for commands the command's purpose.</summary>
    public string Text { get; } = text;

    public string? Command { get; init; }

    public int? ExitCode { get; init; }

    public AgentStatus Status { get; init; }
}
