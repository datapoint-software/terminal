namespace Datapoint.Terminal.Agent;

public sealed record AgentOptions
{
    /// <summary>The mode each task starts in, from the connection profile; Auto falls back to it when a task ends.</summary>
    public ApprovalMode DefaultMode { get; init; } = ApprovalMode.Ask;

    /// <summary>How long to wait for a command to finish before reporting it as still running.</summary>
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Output beyond this is cut to its start and end before it's sent to the provider.</summary>
    public int MaxOutputCharacters { get; init; } = 16_000;

    /// <summary>Stops a task that keeps calling tools, e.g. a model stuck in a loop.</summary>
    public int MaxStepsPerTask { get; init; } = 50;

    /// <summary>The longest reply the model may write in one step.</summary>
    public int MaxOutputTokens { get; init; } = 16_000;
}
