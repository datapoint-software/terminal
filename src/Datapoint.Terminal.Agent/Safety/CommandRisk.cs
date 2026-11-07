namespace Datapoint.Terminal.Agent.Safety;

public enum CommandRisk
{
    ReadOnly,

    Changes,

    // Decides which machines and keys are trusted, so it asks even in Plan mode (product decision 84); only ignoring permissions skips it.
    AlwaysAsk,
}
