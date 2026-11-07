namespace Datapoint.Terminal.Settings;

// Where the memory Settings shows is kept: the user's own, for every workspace, and the workspace's whose window Settings was
// opened from. A window that only reads its workspace, because another holds it, can't change that workspace's memory.
public sealed record MemoryPlaces(string GlobalPath, string? WorkspaceName, string? WorkspacePath, bool CanWriteWorkspace);
