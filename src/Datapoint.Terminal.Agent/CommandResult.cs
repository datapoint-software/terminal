namespace Datapoint.Terminal.Agent;

public sealed record CommandResult(string? CommandLine, string Output, bool IsFinished, int? ExitCode, bool IsAgentCommand);
