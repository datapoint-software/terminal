namespace Datapoint.Terminal.Agent;

public sealed record AgentQuestion(string Text, IReadOnlyList<string> Options);
