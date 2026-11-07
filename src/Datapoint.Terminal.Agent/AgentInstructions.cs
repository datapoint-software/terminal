using Datapoint.Terminal.Agent.Projects;

namespace Datapoint.Terminal.Agent;

// Instructions are the user's own files; data are files the user hasn't trusted, which the model sees but mustn't follow.
public sealed record AgentInstructions(IReadOnlyList<InstructionFile> Instructions, IReadOnlyList<InstructionFile> Data)
{
    public static AgentInstructions None { get; } = new([], []);
}
