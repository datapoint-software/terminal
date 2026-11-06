namespace Datapoint.Terminal.Agent;

public enum AgentStatus
{
    Idle,
    Thinking,
    RunningCommand,
    WaitingForApproval,
    WaitingForUser,
    Interrupted,
}
