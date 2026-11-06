namespace Datapoint.Terminal.Agent.Projects;

public sealed record ProjectSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public IReadOnlyList<ProjectConnection> Connections { get; init => field = value ?? []; } = [];

    public string? HostsFolder { get; init; }

    public string? RunbooksFolder { get; init; }
}
