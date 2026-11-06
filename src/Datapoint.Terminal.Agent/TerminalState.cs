namespace Datapoint.Terminal.Agent;

/// <summary>
/// A snapshot of the terminal. <see cref="HasShellIntegration"/> means the shell reports prompts and commands, so
/// <see cref="IsAtPrompt"/> and <see cref="PendingInput"/> can be trusted; without it the agent can only guess.
/// </summary>
public sealed record TerminalState(bool HasShellIntegration, bool IsAtPrompt, string PendingInput, bool IsAlternateScreen, string? WorkingDirectory, long NextOutputLine);
