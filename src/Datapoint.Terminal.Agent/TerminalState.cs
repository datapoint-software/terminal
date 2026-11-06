namespace Datapoint.Terminal.Agent;

// IsAtPrompt and PendingInput can be trusted only with shell integration; without it the agent can only guess.
public sealed record TerminalState(bool HasShellIntegration, bool IsAtPrompt, string PendingInput, bool IsAlternateScreen, string? WorkingDirectory, long NextOutputLine);
