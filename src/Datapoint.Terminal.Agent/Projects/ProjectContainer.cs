using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Projects;

// A Docker container or Kubernetes pod a connection opens a shell in, on the local machine or on its SSH host. The fields come
// from a shared file and end up in a command line, so each is held to the characters the container tools themselves allow.
public sealed record ProjectContainer
{
    // bash where the image has it, since most images that have it are meant to be used with it; sh otherwise.
    private const string BestShell = "if command -v bash >/dev/null 2>&1; then exec bash; else exec sh; fi";

    public ContainerKind Kind { get; init; } = ContainerKind.Docker;

    public required string Name { get; init; }

    public string? Namespace { get; init; }

    public string? Shell { get; init; }

    public string Describe() => Kind == ContainerKind.Kubernetes ? "Kubernetes pod" : "Docker container";

    public string Address() => Kind == ContainerKind.Kubernetes
        ? "kubernetes:" + (Namespace is null ? string.Empty : Namespace + "/") + Name
        : "docker:" + Name;

    public IReadOnlyList<string> Command()
    {
        string[] shell = Shell is null ? ["sh", "-c", BestShell] : [Shell];
        return Kind == ContainerKind.Kubernetes
            ? ["kubectl", "exec", "-it", .. Namespace is null ? Array.Empty<string>() : ["-n", Namespace], Name, "--", .. shell]
            : ["docker", "exec", "-it", Name, .. shell];
    }

    // For a POSIX shell, locally or on the SSH host.
    public string CommandLine() => string.Join(' ', Command().Select(ShellText.Quote));

    public string? Problem(string connection) =>
        !IsName(Name, Kind == ContainerKind.Kubernetes ? 253 : 128) ? $"the container name of {connection} must be letters, digits, dots, dashes or underscores, starting with a letter or digit."
        : Namespace is not null && (Kind != ContainerKind.Kubernetes || !IsName(Namespace, 63)) ? $"the namespace of {connection} must be a Kubernetes namespace name, and only Kubernetes pods have one."
        : Shell is not null && !IsShellPath(Shell) ? $"the container shell of {connection} must be a program name or path, like bash or /bin/sh."
        : null;

    private static bool IsName(string text, int maximum) =>
        text.Length is > 0 && text.Length <= maximum
        && char.IsAsciiLetterOrDigit(text[0])
        && text.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    private static bool IsShellPath(string text) =>
        text.Length is > 0 and <= 100
        && text[0] != '-'
        && !text.Contains("..", StringComparison.Ordinal)
        && text.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '.' or '-' or '_');
}
