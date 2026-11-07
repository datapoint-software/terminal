namespace Datapoint.Terminal.Agent;

// Details are the background the user needs to decide, kept apart so the question itself stays one short line.
public sealed record AgentQuestion(string Text, IReadOnlyList<AgentOption> Options)
{
    public string? Details { get; init; }
}
