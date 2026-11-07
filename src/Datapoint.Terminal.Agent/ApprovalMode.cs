namespace Datapoint.Terminal.Agent;

// One mode for the agent, across every terminal (product decision 107).
public enum ApprovalMode
{
    Ask,

    AskForRisky,

    // Changes wait for an approved plan, whose steps then run without asking; others still ask.
    Plan,

    // Nothing asks except web access and commands that touch files the workspace keeps from the agent.
    IgnorePermissions,
}
