namespace Datapoint.Terminal.Agent.Projects;

// The connections of one project as the user last confirmed them, kept in the application's own data (product decision 92).
public sealed record ConfirmedConnections
{
    public required string Folder { get; init; }

    public IReadOnlyList<ProjectConnection> Connections { get; init => field = value ?? []; } = [];
}
