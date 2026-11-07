using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Projects.Ignore;

namespace Datapoint.Terminal.Agent;

// Instructions are the user's own files; data are files the user hasn't trusted, which the model sees but mustn't follow.
public sealed record AgentInstructions(IReadOnlyList<InstructionFile> Instructions, IReadOnlyList<InstructionFile> Data)
{
    // The .terminalignore files that apply to the project, so the agent can avoid reading those files in the terminal.
    public IReadOnlyList<IgnoredFiles> Ignored { get; init; } = [];

    // The agent's own notes, global and, in a trusted project, the project's; the user's instruction files take precedence.
    public IReadOnlyList<InstructionFile> Memory { get; init; } = [];

    public static AgentInstructions None { get; } = new([], []);
}
