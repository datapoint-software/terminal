namespace Datapoint.Terminal.Agent.Skills;

public enum RolloutHostState
{
    // Its turn hasn't come; the agent may look but not change anything there.
    Waiting,

    // The agent is carrying out the skill's steps there.
    Working,

    // The steps are done and the application is running the health check.
    Checking,

    // The terminal went away, e.g. for a reboot, and the application is opening it again.
    Reconnecting,

    Done,

    Failed,

    // Never started, because the rollout stopped first or the user chose to skip it.
    Skipped,
}
