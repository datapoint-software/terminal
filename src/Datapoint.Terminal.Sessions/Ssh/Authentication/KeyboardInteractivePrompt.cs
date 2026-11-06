namespace Datapoint.Terminal.Sessions.Ssh.Authentication;

/// <summary>One question from the server; when <see cref="Echo"/> is false the answer is a secret and must be masked.</summary>
public sealed record KeyboardInteractivePrompt(string Text, bool Echo);
