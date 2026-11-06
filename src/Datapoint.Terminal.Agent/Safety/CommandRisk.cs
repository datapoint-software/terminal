namespace Datapoint.Terminal.Agent.Safety;

public enum CommandRisk
{
    ReadOnly,

    Changes,

    // Decides which machines and keys are trusted, so it always asks, even in Auto (product decision 84).
    AlwaysAsk,
}
