namespace Datapoint.Terminal.Sessions.Ssh.Authentication;

// When Echo is false the answer is a secret and must be masked.
public sealed record KeyboardInteractivePrompt(string Text, bool Echo);
