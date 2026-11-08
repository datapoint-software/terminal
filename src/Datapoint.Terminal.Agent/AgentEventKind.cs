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

    // The user approved a step added to the plan: Plan is the plan with it, PlanStep its number.
    PlanStepAdded,

    // A skill's rollout started or moved on; Rollout holds where it is.
    RolloutChanged,

    // A web search or page read finished: Text is the query or address, WebKind which it was, Sources what it found.
    WebUsed,
}
