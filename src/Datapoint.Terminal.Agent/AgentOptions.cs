namespace Datapoint.Terminal.Agent;

public sealed record AgentOptions
{
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(60);

    public int MaxOutputCharacters { get; init; } = 16_000;

    // Stops a task that keeps calling tools, e.g. a model stuck in a loop.
    public int MaxStepsPerTask { get; init; } = 50;

    public int MaxOutputTokens { get; init; } = 16_000;
}
