namespace Datapoint.Terminal.Agent.Safety;

public enum CommandRisk
{
    /// <summary>Only reads; runs on its own in "Ask for risky commands" mode.</summary>
    ReadOnly,

    /// <summary>May change the system; asks in "Ask for risky commands" mode.</summary>
    Changes,

    /// <summary>Decides which machines and keys are trusted; always asks, even in Auto (product decision 84).</summary>
    AlwaysAsk,
}
