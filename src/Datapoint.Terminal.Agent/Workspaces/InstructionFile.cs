namespace Datapoint.Terminal.Agent.Workspaces;

// A reviewed file has been through the secret check with the user, so its text is sent as it is.
public sealed record InstructionFile(string Path, string Text, bool IsCut, string? Problem = null, bool IsReviewed = false);
