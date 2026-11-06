namespace Datapoint.Terminal.Sessions.ShellIntegration;

public sealed record ShellLaunch(string Program, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> Environment, ShellKind Kind);
