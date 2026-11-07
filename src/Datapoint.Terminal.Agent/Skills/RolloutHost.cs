namespace Datapoint.Terminal.Agent.Skills;

// One host's place in a rollout, as the chat's rollout card shows it.
public sealed record RolloutHost(string Name, RolloutHostState State)
{
    // Why it failed, or how the check is going, in a short line.
    public string? Note { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    // How long the health check has kept passing, while it's checked.
    public TimeSpan? HealthyFor { get; init; }

    // It failed its health check rather than its steps, so checking again can help.
    public bool FailedCheck { get; init; }

    public bool IsOver => State is RolloutHostState.Done or RolloutHostState.Failed or RolloutHostState.Skipped;
}
