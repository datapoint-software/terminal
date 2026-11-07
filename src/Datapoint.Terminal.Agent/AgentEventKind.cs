namespace Datapoint.Terminal.Agent;

public enum AgentEventKind
{
    TextDelta,

    Reply,

    CommandStarted,

    CommandFinished,

    Notice,

    StatusChanged,

    MemoryChanged,

    HostDocsChanged,

    UsageChanged,

    PlanApproved,

    // A skill's rollout started or moved on; Rollout holds where it is.
    RolloutChanged,
}
