namespace Datapoint.Terminal.Agent;

// Details are the background the user needs to decide, kept apart so the question itself stays one short line.
public sealed record AgentQuestion(string Text, IReadOnlyList<AgentOption> Options)
{
    public string? Details { get; init; }

    // The terminal it's about, and the session started in it, if any, so the user knows which machine it concerns.
    public string? Terminal { get; init; }

    public string? Session { get; init; }
}
