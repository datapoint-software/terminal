namespace Datapoint.Terminal.Sessions.ShellIntegration;

/// <summary>How to start a shell so it loads our integration: its arguments and the environment to add.</summary>
public sealed record ShellLaunch(string Program, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> Environment, ShellKind Kind);
