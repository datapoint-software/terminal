namespace Datapoint.Terminal.Agent;

public enum ApprovalMode
{
    Ask,

    AskForRisky,

    // Commands that always ask still ask; lasts for one task.
    Auto,
}
