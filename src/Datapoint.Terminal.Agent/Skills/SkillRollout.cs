namespace Datapoint.Terminal.Agent.Skills;

// How a skill moves through its hosts (product decision 103). The health check is a command run on each host after the steps:
// it must keep passing (exit status 0) for HealthyFor before the next host starts, and give up after Timeout.
public sealed record SkillRollout
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(24);

    public RolloutOrder Order { get; init; } = RolloutOrder.OneByOne;

    // Hosts at a time, for Batches.
    public int BatchSize { get; init; } = 2;

    // The most hosts at once, for AllAtOnce, so a label that grows doesn't change every host in one go.
    public int MaxHosts { get; init; } = 10;

    public string? HealthCheck { get; init; }

    public TimeSpan HealthyFor { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(15);

    public RolloutFailure OnFailure { get; init; } = RolloutFailure.Stop;

    // How many hosts start together.
    public int Width(int hosts) => Order switch
    {
        RolloutOrder.OneByOne => 1,
        RolloutOrder.Batches => Math.Clamp(BatchSize, 1, Math.Max(hosts, 1)),
        _ => Math.Clamp(MaxHosts, 1, Math.Max(hosts, 1)),
    };

    // Whether this rollout is less careful than another: more hosts at once, a shorter or missing check, or carrying on after a
    // failure. Shown when a skill changes from outside (product decision 101).
    public bool IsLooserThan(SkillRollout other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Width(int.MaxValue) > other.Width(int.MaxValue)
            || (HealthCheck is null && other.HealthCheck is not null)
            || (HealthCheck is not null && other.HealthCheck is not null && HealthyFor < other.HealthyFor)
            || Rank(OnFailure) > Rank(other.OnFailure);
    }

    private static int Rank(RolloutFailure failure) => failure switch
    {
        RolloutFailure.Stop => 0,
        RolloutFailure.Ask => 1,
        _ => 2,
    };
}
