namespace Datapoint.Terminal.Agent;

public enum PlanStepState
{
    Pending,

    Running,

    Succeeded,

    Failed,

    // Over, but the shell reported no exit code, so whether it worked is for the output to say.
    Finished,
}
