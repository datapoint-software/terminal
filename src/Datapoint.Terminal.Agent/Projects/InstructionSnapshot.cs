namespace Datapoint.Terminal.Agent.Projects;

// The file's text with secrets masked, and a fingerprint of the text as it was, so a change only to a secret is still noticed.
public sealed record InstructionSnapshot(string Text, string Fingerprint);
