namespace Datapoint.Terminal.Agent.Safety;

[Flags]
public enum StepHazards
{
    None = 0,

    EndsConnection = 1,

    ErasesDisk = 2,

    DeletesRecursively = 4,

    ChangesTrust = 8,
}
