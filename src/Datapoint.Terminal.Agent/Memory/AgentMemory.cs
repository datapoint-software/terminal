namespace Datapoint.Terminal.Agent.Memory;

// Where the agent may write its notes; a path is null where it can't, e.g. the project's in a read-only window.
public sealed record AgentMemory(string? GlobalPath, string? ProjectPath)
{
    public string? PathFor(MemoryScope scope) => scope == MemoryScope.Global ? GlobalPath : ProjectPath;
}
