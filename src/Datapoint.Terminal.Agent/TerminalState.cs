namespace Datapoint.Terminal.Agent;

// IsAtPrompt and PendingInput can be trusted only with shell integration; without it the agent can only guess.
public sealed record TerminalState(bool HasShellIntegration, bool IsAtPrompt, string PendingInput, bool IsAlternateScreen, string? WorkingDirectory, long NextOutputLine)
{
    // An interactive session started in the terminal's shell, e.g. "ssh session to web1", whose prompt the agent is typing at.
    public string? Session { get; init; }

    // A program is waiting for input it doesn't show, such as a password, which only the user types.
    public bool IsInputHidden { get; init; }
}
