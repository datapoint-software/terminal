namespace Datapoint.Terminal.Agent.Workspaces;

// Kept in the application's own data, never in the workspace, so nothing in a workspace folder can vouch for itself.
public sealed record WorkspaceTrustRecord
{
    public required string Folder { get; init; }

    public bool IsTrusted { get; init; }

    // The instruction files the user saw when deciding; a file that isn't listed here is read as data until the user decides again.
    public IReadOnlyList<string> InstructionFiles { get; init => field = value ?? []; } = [];

    public bool Covers(string path) => InstructionFiles.Contains(path, WorkspaceFolders.Comparer);
}
