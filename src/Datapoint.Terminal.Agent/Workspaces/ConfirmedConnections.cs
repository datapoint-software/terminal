namespace Datapoint.Terminal.Agent.Workspaces;

// The connections of one workspace as the user last confirmed them, kept in the application's own data (product decision 92).
public sealed record ConfirmedConnections
{
    public required string Folder { get; init; }

    public IReadOnlyList<WorkspaceConnection> Connections { get; init => field = value ?? []; } = [];
}
