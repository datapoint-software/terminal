namespace Datapoint.Terminal.Agent.Skills;

// What happens to the hosts still to go when one fails.
public enum RolloutFailure
{
    Stop,
    Continue,
    Ask,
}
