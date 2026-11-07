namespace Datapoint.Terminal.Agent.Skills;

// A rollout at one moment, for the chat's card. Id tells one rollout from the next.
public sealed record RolloutProgress(Guid Id, string Skill, string Title, SkillRollout Rollout, IReadOnlyList<RolloutHost> Hosts)
{
    // The batch being worked on, from 1.
    public int Batch { get; init; }

    public int BatchCount { get; init; }

    public bool IsPaused { get; init; }

    public RolloutOutcome Outcome { get; init; }

    public bool IsOver => Outcome != RolloutOutcome.Running;
}
