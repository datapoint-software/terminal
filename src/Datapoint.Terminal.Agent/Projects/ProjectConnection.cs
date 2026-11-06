namespace Datapoint.Terminal.Agent.Projects;

public sealed record ProjectConnection
{
    public required string Name { get; init; }

    public string? SshHost { get; init; }

    public string? Host { get; init; }

    public string? User { get; init; }

    public int? Port { get; init; }

    public string? Environment { get; init; }

    public ApprovalMode? ApprovalMode { get; init; }

    public string? Model { get; init; }
}
