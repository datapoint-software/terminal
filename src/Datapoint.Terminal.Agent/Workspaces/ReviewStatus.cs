namespace Datapoint.Terminal.Agent.Workspaces;

public enum ReviewStatus
{
    // As the user last accepted it, or written by the application.
    Accepted,

    // Changed from outside since the user last accepted it.
    Changed,

    // Never accepted: it arrived from outside.
    New,
}
