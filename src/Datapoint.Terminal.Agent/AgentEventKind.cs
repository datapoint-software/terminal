namespace Datapoint.Terminal.Agent;

public enum AgentEventKind
{
    /// <summary>More of the agent's reply, as it streams.</summary>
    TextDelta,

    /// <summary>The agent finished a reply.</summary>
    Reply,

    /// <summary>The agent typed a command into the terminal.</summary>
    CommandStarted,

    /// <summary>A command the agent typed has finished, or stopped being waited for.</summary>
    CommandFinished,

    /// <summary>Something the user should know, from the application rather than the model (e.g. "Interrupted").</summary>
    Notice,

    StatusChanged,
}
