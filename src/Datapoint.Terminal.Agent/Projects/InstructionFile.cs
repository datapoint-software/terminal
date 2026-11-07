namespace Datapoint.Terminal.Agent.Projects;

public sealed record InstructionFile(string Path, string Text, bool IsCut, string? Problem = null);
