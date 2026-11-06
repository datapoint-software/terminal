namespace Datapoint.Terminal.Agent;

/// <summary>A command's output and outcome. <see cref="IsFinished"/> is false while it's still running.</summary>
public sealed record CommandResult(string? CommandLine, string Output, bool IsFinished, int? ExitCode, bool IsAgentCommand);
