namespace Datapoint.Terminal.Sessions.Ssh.Authentication;

public sealed record KeyboardInteractiveRequest(string Name, string Instruction, IReadOnlyList<KeyboardInteractivePrompt> Prompts);
