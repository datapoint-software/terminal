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
}
