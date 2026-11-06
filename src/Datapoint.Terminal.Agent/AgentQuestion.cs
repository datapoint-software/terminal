namespace Datapoint.Terminal.Agent;

/// <summary>A question for the user; with options, the user picks one (or writes their own answer).</summary>
public sealed record AgentQuestion(string Text, IReadOnlyList<string> Options);
