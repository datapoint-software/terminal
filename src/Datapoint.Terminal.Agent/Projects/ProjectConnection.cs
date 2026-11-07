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

    // A container on the connection's host, or on the local machine when it names no host.
    public ProjectContainer? Container { get; init; }

    public bool IsLocal => SshHost is null && Host is null;

    // Ports forwarded while a tab is open, written as LocalForward is: "8080 localhost:80".
    public IReadOnlyList<string>? Forwards { get; init; }

    // False for a host whose shell shouldn't be started through our scripts, e.g. one that logs every command run at login.
    public bool? ShellIntegration { get; init; }

    public bool IsProduction => EnvironmentLabels.IsProduction(Environment);

    // Names and labels come from a shared file and reach the model and the screen, so they're kept to short, plain text that
    // can't carry instructions or disguise itself.
    public static bool IsPlainLabel(string? text) =>
        text is { Length: > 0 and <= 40 }
        && text.All(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '-' or '_' or '.' or '@')
        && !char.IsWhiteSpace(text[0])
        && !char.IsWhiteSpace(text[^1]);

    public string? Problem() =>
        !IsPlainLabel(Name) ? "a connection's name must be up to 40 letters, digits, spaces, dots, dashes, underscores or @."
        : Environment is not null && !IsPlainLabel(Environment) ? $"the environment label of {Name} must be up to 40 letters, digits, spaces, dots, dashes, underscores or @."
        : SshHost is not null && !IsPlainLabel(SshHost) ? $"the SSH host of {Name} isn't a plain host name."
        : PlaceProblem();

    private string? PlaceProblem() =>
        IsLocal && Container is null ? $"{Name} names neither an SSH host, a host nor a container."
        : Container?.Problem(Name) ?? ForwardsProblem();

    private string? ForwardsProblem() =>
        Forwards is not { Count: > 0 } ? null
        : IsLocal ? $"{Name} forwards ports but names no SSH host to forward them through."
        : Forwards.FirstOrDefault(forward => Sessions.Ssh.Config.SshForward.Parse(forward) is null) is { } wrong ? $"the forward \"{wrong}\" of {Name} isn't written as port host:port."
        : null;
}
