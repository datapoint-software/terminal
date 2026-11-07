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

    public bool IsProduction => Environment?.StartsWith("prod", StringComparison.OrdinalIgnoreCase) == true;

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
        : SshHost is null && Host is null ? $"{Name} names neither an SSH host nor a host."
        : null;
}
