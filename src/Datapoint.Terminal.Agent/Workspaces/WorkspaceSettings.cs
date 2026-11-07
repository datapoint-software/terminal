namespace Datapoint.Terminal.Agent.Workspaces;

public sealed record WorkspaceSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public IReadOnlyList<WorkspaceConnection> Connections { get; init => field = value ?? []; } = [];

    public string? HostsFolder { get; init; }

    public string? RunbooksFolder { get; init; }
}
