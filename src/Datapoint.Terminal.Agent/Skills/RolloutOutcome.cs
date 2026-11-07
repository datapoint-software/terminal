namespace Datapoint.Terminal.Agent.Skills;

public enum RolloutOutcome
{
    Running,

    Completed,

    // Stopped by a failure, as the skill's on-failure setting or the user said.
    Failed,

    // Stopped by the user from the rollout card.
    Stopped,
}
