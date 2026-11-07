namespace Datapoint.Terminal.Agent.Safety;

// Line counts from 1.
public sealed record SecretMatch(int Index, int Length, string Value, int Line);
